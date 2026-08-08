using System.Net;
using FleetRental.Domain.Enums;

namespace FleetRental.IntegrationTests;

/// <summary>
/// Per-category demand forecasting, end to end. The trend rules and the forecast
/// itself are covered by CategoryDemandAnalyzerTests against synthetic series;
/// this suite proves the pipeline around them — which categories get reported,
/// the capacity figure beside each, authorization, and tenant isolation.
/// </summary>
/// <remarks>
/// The sufficient-history path cannot be exercised here for the same reason as
/// the revenue forecast: <c>Booking.Request</c> refuses a past start date by
/// domain rule, so months of settled history cannot be created through the API,
/// and reaching around that with a raw insert would test a state the real system
/// can never reach.
/// </remarks>
[Collection(nameof(ApiCollection))]
public class CategoryDemandTests(FleetRentalApiFactory factory) : IAsyncLifetime
{
    private const string Endpoint = "/api/analytics/category-demand";

    public async Task InitializeAsync()
    {
        await factory.ResetAsync();
        await factory.SeedTenantAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<ApiClient> SeedAdminAsync()
    {
        var (email, password) = await factory.SeedAdminAsync();
        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(email, password, asAdmin: true));
        return admin;
    }

    // ---------- Authorization ----------

    [Fact]
    public async Task A_client_cannot_read_category_demand()
    {
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");

        var response = await client.Http.GetAsync(Endpoint);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_read_category_demand()
    {
        var anonymous = factory.CreateTenantClient();

        var response = await anonymous.Http.GetAsync(Endpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Which categories are reported ----------

    [Fact]
    public async Task A_fleet_with_no_cars_reports_nothing()
    {
        var admin = await SeedAdminAsync();

        var result = await admin.GetAsync<ApiClient.CategoryDemandResult[]>(Endpoint);

        Assert.Empty(result!);
    }

    [Fact]
    public async Task Only_categories_the_fleet_actually_owns_are_reported()
    {
        // Seven categories exist in the enum; a fleet of vans and one SUV should
        // hear about two of them, not be told a bus it does not own has flat demand.
        await factory.SeedCarAsync(name: "Van A", category: CarCategory.Van);
        await factory.SeedCarAsync(name: "Van B", category: CarCategory.Van);
        await factory.SeedCarAsync(name: "Big SUV", category: CarCategory.Suv);
        var admin = await SeedAdminAsync();

        var result = await admin.GetAsync<ApiClient.CategoryDemandResult[]>(Endpoint);

        Assert.Equal(2, result!.Length);
        Assert.Contains(result, r => r.Category == "Van");
        Assert.Contains(result, r => r.Category == "Suv");
        Assert.DoesNotContain(result, r => r.Category == "Bus");
    }

    [Fact]
    public async Task Each_category_reports_how_many_cars_the_fleet_holds()
    {
        // The capacity half of "should I buy another one of these".
        await factory.SeedCarAsync(name: "Van A", category: CarCategory.Van);
        await factory.SeedCarAsync(name: "Van B", category: CarCategory.Van);
        await factory.SeedCarAsync(name: "Van C", category: CarCategory.Van);
        await factory.SeedCarAsync(name: "Lone Sedan", category: CarCategory.Sedan);
        var admin = await SeedAdminAsync();

        var result = await admin.GetAsync<ApiClient.CategoryDemandResult[]>(Endpoint);

        Assert.Equal(3, Array.Find(result!, r => r.Category == "Van")!.CarCount);
        Assert.Equal(1, Array.Find(result!, r => r.Category == "Sedan")!.CarCount);
    }

    [Fact]
    public async Task Retired_cars_still_count_toward_capacity()
    {
        // A retired car is capital the fleet still owns; excluding it would
        // understate capacity in exactly the decision this screen supports.
        await factory.SeedCarAsync(name: "Active Van", category: CarCategory.Van);
        await factory.SeedCarAsync(name: "Retired Van", status: CarStatus.Retired, category: CarCategory.Van);
        var admin = await SeedAdminAsync();

        var result = await admin.GetAsync<ApiClient.CategoryDemandResult[]>(Endpoint);

        Assert.Equal(2, Array.Find(result!, r => r.Category == "Van")!.CarCount);
    }

    // ---------- Cold start ----------

    [Fact]
    public async Task A_fleet_with_cars_but_no_bookings_reports_unknown_not_steady()
    {
        // "Unknown" and "Steady" must stay distinct: one says the fleet has no
        // idea yet, the other claims demand is holding.
        await factory.SeedCarAsync(name: "Idle Van", category: CarCategory.Van);
        var admin = await SeedAdminAsync();

        var result = await admin.GetAsync<ApiClient.CategoryDemandResult[]>(Endpoint);

        var van = Assert.Single(result!);
        Assert.False(van.HasSufficientHistory);
        Assert.Equal("Unknown", van.Trend);
        Assert.Empty(van.Forecast);
        Assert.Equal(1, van.CarCount);
    }

    [Fact]
    public async Task A_only_future_booking_leaves_history_insufficient()
    {
        // Every booking a fresh fleet can legally create starts today or later, so
        // there is no settled month to train on however many are made.
        var carId = await factory.SeedCarAsync(name: "Van", category: CarCategory.Van);
        var admin = await SeedAdminAsync();
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var bookingId = await client.CreateBookingAndGetIdAsync(carId, today.AddDays(10), today.AddDays(14));
        await admin.ApproveAsync(bookingId);

        var result = await admin.GetAsync<ApiClient.CategoryDemandResult[]>(Endpoint);

        var van = Assert.Single(result!);
        Assert.False(van.HasSufficientHistory);
        Assert.Equal("Unknown", van.Trend);
    }

    // ---------- Tenant isolation ----------

    [Fact]
    public async Task Category_demand_never_mixes_fleets_across_tenants()
    {
        var alpha = await factory.SeedTenantAsync("alpha-rentals");
        var beta = await factory.SeedTenantAsync("beta-motors");

        await factory.SeedCarAsync(alpha, "Alpha Van", category: CarCategory.Van);
        await factory.SeedCarAsync(alpha, "Alpha SUV", category: CarCategory.Suv);
        await factory.SeedCarAsync(beta, "Beta Bus", category: CarCategory.Bus);

        var (betaEmail, betaPassword) = await factory.SeedAdminAsync(beta, "admin@beta.com");
        var betaAdmin = factory.CreateTenantClient("beta-motors");
        betaAdmin.Authenticate(await betaAdmin.LoginAsync(betaEmail, betaPassword, asAdmin: true));

        var result = await betaAdmin.GetAsync<ApiClient.CategoryDemandResult[]>(Endpoint);

        var bus = Assert.Single(result!);
        Assert.Equal("Bus", bus.Category);
        Assert.Equal(1, bus.CarCount);
    }
}
