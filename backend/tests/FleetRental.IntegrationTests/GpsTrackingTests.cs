using System.Net;
using System.Net.Http.Json;
using FleetRental.Domain.Enums;

namespace FleetRental.IntegrationTests;

/// <summary>
/// GPS ingestion from a car's physical device, the fleet-wide "latest location"
/// read for the owner's map, and device-key management. The security-critical
/// boundaries: an unknown key is rejected, a disabled Gps feature 403s both
/// sides, and one tenant's locations never surface in another's map.
/// </summary>
[Collection(nameof(ApiCollection))]
public class GpsTrackingTests(FleetRentalApiFactory factory) : IAsyncLifetime
{
    public async Task InitializeAsync() => await factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_valid_device_key_stores_a_reading_and_it_shows_up_on_the_map()
    {
        var tenantId = await factory.SeedTenantAsync();
        var carId = await factory.SeedCarAsync(tenantId);
        await factory.SetGpsDeviceKeyAsync(carId, "device-key-1");

        var device = factory.CreatePlatformClient(); // plain client, no tenant header, no token — same as real hardware
        var reportResponse = await device.ReportLocationAsync("device-key-1", 25.276987, 55.296249);
        Assert.Equal(HttpStatusCode.NoContent, reportResponse.StatusCode);

        var (email, password) = await factory.SeedAdminAsync(tenantId);
        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(email, password, asAdmin: true));

        var mapResponse = await admin.GetCarLocationsAsync();
        mapResponse.EnsureSuccessStatusCode();
        var locations = await mapResponse.Content.ReadFromJsonAsync<List<ApiClient.CarLocationResult>>();

        Assert.NotNull(locations);
        var reading = Assert.Single(locations, l => l.CarId == carId);
        Assert.Equal(25.276987, reading.Latitude, precision: 5);
        Assert.Equal(55.296249, reading.Longitude, precision: 5);
    }

    [Fact]
    public async Task An_unknown_device_key_is_rejected()
    {
        var device = factory.CreatePlatformClient();

        var response = await device.ReportLocationAsync("not-a-real-key", 25.2, 55.3);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_out_of_range_coordinate_is_rejected()
    {
        var tenantId = await factory.SeedTenantAsync();
        var carId = await factory.SeedCarAsync(tenantId);
        await factory.SetGpsDeviceKeyAsync(carId, "device-key-2");

        var device = factory.CreatePlatformClient();
        var response = await device.ReportLocationAsync("device-key-2", latitude: 200, longitude: 55.3);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Disabling_gps_rejects_the_ingestion_endpoint_and_the_map_read()
    {
        var tenantId = await factory.SeedTenantAsync();
        var carId = await factory.SeedCarAsync(tenantId);
        await factory.SetGpsDeviceKeyAsync(carId, "device-key-3");
        await factory.SetFeatureAsync(tenantId, FeatureKey.Gps, isEnabled: false);

        var device = factory.CreatePlatformClient();
        var reportResponse = await device.ReportLocationAsync("device-key-3", 25.2, 55.3);
        Assert.Equal(HttpStatusCode.Forbidden, reportResponse.StatusCode);

        var (email, password) = await factory.SeedAdminAsync(tenantId);
        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(email, password, asAdmin: true));

        var mapResponse = await admin.GetCarLocationsAsync();
        Assert.Equal(HttpStatusCode.Forbidden, mapResponse.StatusCode);
    }

    [Fact]
    public async Task A_companys_locations_never_appear_on_another_companys_map()
    {
        var alpha = await factory.SeedTenantAsync("alpha-gps");
        var beta = await factory.SeedTenantAsync("beta-gps");
        var alphaCarId = await factory.SeedCarAsync(alpha, "Alpha Car");
        var betaCarId = await factory.SeedCarAsync(beta, "Beta Car");
        await factory.SetGpsDeviceKeyAsync(alphaCarId, "alpha-device-key");
        await factory.SetGpsDeviceKeyAsync(betaCarId, "beta-device-key");

        var device = factory.CreatePlatformClient();
        (await device.ReportLocationAsync("alpha-device-key", 25.0, 55.0)).EnsureSuccessStatusCode();
        (await device.ReportLocationAsync("beta-device-key", 26.0, 56.0)).EnsureSuccessStatusCode();

        var (alphaEmail, alphaPassword) = await factory.SeedAdminAsync(alpha, "alpha-admin@test.com");
        var alphaAdmin = factory.CreateTenantClient("alpha-gps");
        alphaAdmin.Authenticate(await alphaAdmin.LoginAsync(alphaEmail, alphaPassword, asAdmin: true));

        var mapResponse = await alphaAdmin.GetCarLocationsAsync();
        mapResponse.EnsureSuccessStatusCode();
        var locations = await mapResponse.Content.ReadFromJsonAsync<List<ApiClient.CarLocationResult>>();

        Assert.NotNull(locations);
        Assert.Single(locations);
        Assert.Equal(alphaCarId, locations[0].CarId);
    }

    [Fact]
    public async Task Regenerating_the_device_key_invalidates_the_old_one()
    {
        var tenantId = await factory.SeedTenantAsync();
        var carId = await factory.SeedCarAsync(tenantId);
        await factory.SetGpsDeviceKeyAsync(carId, "old-key");

        var (email, password) = await factory.SeedAdminAsync(tenantId);
        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(email, password, asAdmin: true));

        var newKey = await admin.RegenerateGpsDeviceKeyAndGetAsync(carId);
        Assert.NotEqual("old-key", newKey);

        var device = factory.CreatePlatformClient();
        var oldKeyResponse = await device.ReportLocationAsync("old-key", 25.0, 55.0);
        Assert.Equal(HttpStatusCode.Unauthorized, oldKeyResponse.StatusCode);

        var newKeyResponse = await device.ReportLocationAsync(newKey, 25.0, 55.0);
        Assert.Equal(HttpStatusCode.NoContent, newKeyResponse.StatusCode);
    }

    [Fact]
    public async Task A_client_cannot_read_the_gps_device_key_or_the_map()
    {
        var tenantId = await factory.SeedTenantAsync();
        var carId = await factory.SeedCarAsync(tenantId);
        await factory.SetGpsDeviceKeyAsync(carId, "device-key-4");

        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("browsing@test.com");

        var keyResponse = await client.GetGpsDeviceKeyAsync(carId);
        var mapResponse = await client.GetCarLocationsAsync();

        Assert.Equal(HttpStatusCode.Forbidden, keyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, mapResponse.StatusCode);
    }
}
