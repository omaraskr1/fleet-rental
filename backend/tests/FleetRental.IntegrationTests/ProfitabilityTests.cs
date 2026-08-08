using System.Net;

namespace FleetRental.IntegrationTests;

/// <summary>
/// Per-car profitability: estimated revenue against real maintenance spend, and
/// the keep/review/retire call derived from it. Admin-only, like the rest of
/// analytics.
/// </summary>
/// <remarks>
/// SeedCarAsync fixes DailyRate at 300, which is what makes the revenue
/// arithmetic below predictable rather than incidental.
/// </remarks>
[Collection(nameof(ApiCollection))]
public class ProfitabilityTests(FleetRentalApiFactory factory) : IAsyncLifetime
{
    private const decimal SeededDailyRate = 300m;

    private static DateOnly Day(int offset) => DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(offset);

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

    private static async Task<Guid> BookAndApproveAsync(
        ApiClient admin, ApiClient client, Guid carId, DateOnly start, DateOnly end)
    {
        var bookingId = await client.CreateBookingAndGetIdAsync(carId, start, end);
        await admin.ApproveAsync(bookingId);
        return bookingId;
    }

    // ---------- Authorization ----------

    [Fact]
    public async Task A_client_cannot_read_profitability()
    {
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");

        var response = await client.Http.GetAsync("/api/analytics/profitability");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_read_profitability()
    {
        var anonymous = factory.CreateTenantClient();

        var response = await anonymous.Http.GetAsync("/api/analytics/profitability");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Arithmetic ----------

    [Fact]
    public async Task Net_profit_is_estimated_revenue_minus_maintenance_cost()
    {
        var carId = await factory.SeedCarAsync();
        var admin = await SeedAdminAsync();
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");

        await BookAndApproveAsync(admin, client, carId, Day(1), Day(5)); // 5 days x 300 = 1500
        await admin.LogServiceAsync(carId, Day(-1), "Oil change", 45_000, 400m);

        var rows = await admin.GetAsync<ApiClient.CarProfitabilityResult[]>("/api/analytics/profitability");
        var car = Array.Find(rows!, r => r.CarId == carId);

        Assert.Equal(5 * SeededDailyRate, car!.EstimatedRevenue);
        Assert.Equal(400m, car.MaintenanceCost);
        Assert.Equal(1100m, car.NetProfit);
    }

    [Fact]
    public async Task A_car_that_cost_more_than_it_earned_is_flagged_for_retiring()
    {
        var carId = await factory.SeedCarAsync();
        var admin = await SeedAdminAsync();
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");

        await BookAndApproveAsync(admin, client, carId, Day(1), Day(2)); // 2 days x 300 = 600
        await admin.LogServiceAsync(carId, Day(-1), "Gearbox rebuild", 45_000, 5_000m);

        var rows = await admin.GetAsync<ApiClient.CarProfitabilityResult[]>("/api/analytics/profitability");
        var car = Array.Find(rows!, r => r.CarId == carId);

        Assert.True(car!.NetProfit < 0);
        Assert.Equal("ConsiderRetiring", car.Recommendation);
    }

    [Fact]
    public async Task A_profitable_but_barely_used_car_is_flagged_for_review()
    {
        // Two booked days across the default ~15-month window is far under the
        // 10% idle threshold, while still turning a profit on paper.
        var carId = await factory.SeedCarAsync();
        var admin = await SeedAdminAsync();
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");

        await BookAndApproveAsync(admin, client, carId, Day(1), Day(2));

        var rows = await admin.GetAsync<ApiClient.CarProfitabilityResult[]>("/api/analytics/profitability");
        var car = Array.Find(rows!, r => r.CarId == carId);

        Assert.True(car!.NetProfit > 0);
        Assert.True(car.UtilizationPercent < 10.0);
        Assert.Equal("Review", car.Recommendation);
    }

    [Fact]
    public async Task A_car_with_no_revenue_reports_an_undefined_margin_rather_than_zero_percent()
    {
        // Zero over zero is not a break-even car — presenting it as "0%" would put
        // an idle vehicle next to one that genuinely earned and spent the same.
        var carId = await factory.SeedCarAsync();
        var admin = await SeedAdminAsync();

        var rows = await admin.GetAsync<ApiClient.CarProfitabilityResult[]>("/api/analytics/profitability");
        var car = Array.Find(rows!, r => r.CarId == carId);

        Assert.Equal(0m, car!.EstimatedRevenue);
        Assert.Null(car.ProfitMarginPercent);
    }

    // ---------- Ranking ----------

    [Fact]
    public async Task Cars_are_ranked_worst_net_profit_first()
    {
        var loser = await factory.SeedCarAsync(name: "Money Pit");
        var earner = await factory.SeedCarAsync(name: "Workhorse");
        var admin = await SeedAdminAsync();
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");

        await BookAndApproveAsync(admin, client, earner, Day(1), Day(10));
        await admin.LogServiceAsync(loser, Day(-1), "Engine replacement", 90_000, 8_000m);

        var rows = await admin.GetAsync<ApiClient.CarProfitabilityResult[]>("/api/analytics/profitability");

        Assert.Equal(loser, rows![0].CarId);
        Assert.Equal(earner, rows[^1].CarId);
    }

    // ---------- Tenant isolation ----------

    [Fact]
    public async Task Profitability_never_mixes_data_across_tenants()
    {
        var alpha = await factory.SeedTenantAsync("alpha-rentals");
        var beta = await factory.SeedTenantAsync("beta-motors");

        var alphaCar = await factory.SeedCarAsync(alpha, "Alpha Van");
        await factory.SeedCarAsync(beta, "Beta Truck");

        var (alphaEmail, alphaPassword) = await factory.SeedAdminAsync(alpha, "admin@alpha.com");
        var alphaAdmin = factory.CreateTenantClient("alpha-rentals");
        alphaAdmin.Authenticate(await alphaAdmin.LoginAsync(alphaEmail, alphaPassword, asAdmin: true));

        var alphaClient = factory.CreateTenantClient("alpha-rentals");
        await alphaClient.SignUpAndAuthenticateAsync("client@alpha.com");
        await BookAndApproveAsync(alphaAdmin, alphaClient, alphaCar, Day(1), Day(5));
        await alphaAdmin.LogServiceAsync(alphaCar, Day(-1), "Alpha service", 10_000, 200m);

        var (betaEmail, betaPassword) = await factory.SeedAdminAsync(beta, "admin@beta.com");
        var betaAdmin = factory.CreateTenantClient("beta-motors");
        betaAdmin.Authenticate(await betaAdmin.LoginAsync(betaEmail, betaPassword, asAdmin: true));

        var betaRows = await betaAdmin.GetAsync<ApiClient.CarProfitabilityResult[]>("/api/analytics/profitability");

        Assert.Single(betaRows!);
        Assert.Equal("Beta Truck", betaRows![0].CarName);
        Assert.Equal(0m, betaRows[0].EstimatedRevenue);
        Assert.Equal(0m, betaRows[0].MaintenanceCost);
    }
}
