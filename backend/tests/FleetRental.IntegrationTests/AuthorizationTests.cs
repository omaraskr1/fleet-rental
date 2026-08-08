using System.Net;
using System.Net.Http.Json;

namespace FleetRental.IntegrationTests;

/// <summary>
/// Authorization boundaries. These are the tests that matter most once the system
/// is multi-tenant — a leak here means one rental company seeing another's data.
/// </summary>
[Collection(nameof(ApiCollection))]
public class AuthorizationTests(FleetRentalApiFactory factory) : IAsyncLifetime
{
    private static DateOnly Day(int offset) => DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(offset);

    public async Task InitializeAsync()
    {
        await factory.ResetAsync();

        // Every request now belongs to a tenant, so one has to exist before a
        // client can even sign up. Tests that seed a car or admin get this for
        // free; the ones here that only sign up need it explicitly.
        await factory.SeedTenantAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Browsing_cars_does_not_require_an_account()
    {
        await factory.SeedCarAsync();

        // Anonymous, but still tenant-scoped: the company code identifies whose
        // catalogue is being browsed, and no account is needed to look.
        var response = await factory.CreateTenantClient().Http.GetAsync("/api/cars");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Booking_requires_authentication()
    {
        var carId = await factory.SeedCarAsync();
        var anonymous = factory.CreateTenantClient();

        var response = await anonymous.CreateBookingAsync(carId, Day(5), Day(7));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_client_cannot_read_the_admin_queue()
    {
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("nosy@test.com");

        var response = await client.Http.GetAsync("/api/bookings");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_client_cannot_approve_their_own_booking()
    {
        // The obvious privilege escalation: approve yourself and take the car.
        var carId = await factory.SeedCarAsync();
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("selfapprove@test.com");
        var bookingId = await client.CreateBookingAndGetIdAsync(carId, Day(5), Day(7));

        var response = await client.ApproveAsync(bookingId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await factory.CountBookedDaysAsync(carId));
    }

    [Fact]
    public async Task A_client_cannot_read_another_clients_booking()
    {
        // Booking ids are GUIDs, but authorization must not depend on them being
        // unguessable — the DTO carries the other client's name and email.
        var carId = await factory.SeedCarAsync();

        var owner = factory.CreateTenantClient();
        await owner.SignUpAndAuthenticateAsync("owner@test.com");
        var bookingId = await owner.CreateBookingAndGetIdAsync(carId, Day(5), Day(7));

        var stranger = factory.CreateTenantClient();
        await stranger.SignUpAndAuthenticateAsync("stranger@test.com");

        var response = await stranger.Http.GetAsync($"/api/bookings/{bookingId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_client_cannot_cancel_another_clients_booking()
    {
        var carId = await factory.SeedCarAsync();

        var owner = factory.CreateTenantClient();
        await owner.SignUpAndAuthenticateAsync("cowner@test.com");
        var bookingId = await owner.CreateBookingAndGetIdAsync(carId, Day(5), Day(7));

        var stranger = factory.CreateTenantClient();
        await stranger.SignUpAndAuthenticateAsync("cstranger@test.com");

        var response = await stranger.CancelAsync(bookingId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task My_bookings_returns_only_the_callers_own()
    {
        var carId = await factory.SeedCarAsync();

        var alice = factory.CreateTenantClient();
        await alice.SignUpAndAuthenticateAsync("alice@test.com");
        await alice.CreateBookingAsync(carId, Day(5), Day(7));

        var bob = factory.CreateTenantClient();
        await bob.SignUpAndAuthenticateAsync("bob@test.com");
        await bob.CreateBookingAsync(carId, Day(20), Day(22));

        var aliceBookings = await alice.GetAsync<ApiClient.BookingResult[]>("/api/bookings/mine");
        var bobBookings = await bob.GetAsync<ApiClient.BookingResult[]>("/api/bookings/mine");

        Assert.Single(aliceBookings!);
        Assert.Single(bobBookings!);
        Assert.NotEqual(aliceBookings![0].Id, bobBookings![0].Id);
    }

    [Fact]
    public async Task A_client_cannot_create_a_car()
    {
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("carmaker@test.com");

        var response = await client.Http.PostAsJsonAsync("/api/cars", new
        {
            name = "Stolen Car",
            description = "x",
            category = "Sedan",
            seats = 4,
            dailyRate = 100,
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_client_cannot_read_the_fleet_calendar()
    {
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("fleetpeek@test.com");

        var response = await client.Http.GetAsync("/api/fleet/availability");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Public_signup_cannot_mint_an_administrator()
    {
        // Guards against the role being accepted from request input.
        var response = await factory.CreateTenantClient().Http.PostAsJsonAsync("/api/auth/signup", new
        {
            email = "wannabe@test.com",
            password = "Password123",
            fullName = "Wannabe Admin",
            role = "Admin",
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiClient.AuthResult>();

        Assert.Equal("Client", body!.User.Role);
    }

    [Fact]
    public async Task A_client_account_is_refused_at_the_admin_login()
    {
        var client = factory.CreateTenantClient();
        await client.SignUpAsync("notadmin@test.com");

        var response = await factory.CreateTenantClient().Http.PostAsJsonAsync("/api/auth/admin/login", new
        {
            email = "notadmin@test.com",
            password = "ClientPass123",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_invalid_token_is_rejected()
    {
        var client = factory.CreateTenantClient();
        client.Authenticate("not.a.real.jwt");

        var response = await client.Http.GetAsync("/api/bookings/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_deactivated_account_cannot_log_in()
    {
        var client = factory.CreateTenantClient();
        await client.SignUpAsync("deactivated@test.com");

        await factory.DeactivateUserAsync("deactivated@test.com");

        var response = await factory.CreateTenantClient().Http.PostAsJsonAsync("/api/auth/login", new
        {
            email = "deactivated@test.com",
            password = "ClientPass123",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
