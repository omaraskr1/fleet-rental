using System.Net;

namespace FleetRental.IntegrationTests;

/// <summary>
/// Fleet-wide analytics for the admin dashboard: revenue, utilisation, and
/// event-type demand derived from bookings already in the system, plus
/// maintenance cost from service records. Admin-only end to end, like
/// maintenance.
/// </summary>
[Collection(nameof(ApiCollection))]
public class AnalyticsTests(FleetRentalApiFactory factory) : IAsyncLifetime
{
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

    // ---------- Authorization ----------

    [Fact]
    public async Task A_client_cannot_read_the_overview()
    {
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");

        var response = await client.Http.GetAsync("/api/analytics/overview");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_read_the_overview()
    {
        var anonymous = factory.CreateTenantClient();

        var response = await anonymous.Http.GetAsync("/api/analytics/overview");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Overview ----------

    [Fact]
    public async Task Overview_counts_cars_and_bookings_by_status()
    {
        var carId = await factory.SeedCarAsync();
        await factory.SeedCarAsync(name: "Second Car");
        var admin = await SeedAdminAsync();

        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");

        var approvedId = await client.CreateBookingAndGetIdAsync(carId, Day(1), Day(5));
        var rejectedId = await client.CreateBookingAndGetIdAsync(carId, Day(10), Day(12));
        await admin.ApproveAsync(approvedId);
        await admin.RejectAsync(rejectedId);

        var overview = await admin.GetAsync<ApiClient.AnalyticsOverviewResult>("/api/analytics/overview");

        Assert.Equal(2, overview!.TotalCars);
        Assert.Equal(2, overview.TotalBookings);
        Assert.Equal(1, overview.ApprovedBookings);
        Assert.Equal(1, overview.RejectedBookings);
        Assert.Equal(50.0, overview.ApprovalRatePercent); // 1 approved of 2 decided
    }

    [Fact]
    public async Task Only_approved_bookings_contribute_estimated_revenue()
    {
        // SeedCarAsync's DailyRate is fixed at 300, so 5 approved days -> 1500.
        var carId = await factory.SeedCarAsync();
        var admin = await SeedAdminAsync();
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");

        var approvedId = await client.CreateBookingAndGetIdAsync(carId, Day(1), Day(5));
        await admin.ApproveAsync(approvedId);
        await client.CreateBookingAndGetIdAsync(carId, Day(10), Day(14)); // left pending

        var overview = await admin.GetAsync<ApiClient.AnalyticsOverviewResult>("/api/analytics/overview");

        Assert.Equal(1500m, overview!.EstimatedRevenue);
    }

    [Fact]
    public async Task A_rejected_booking_contributes_no_revenue()
    {
        var carId = await factory.SeedCarAsync();
        var admin = await SeedAdminAsync();
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");

        var bookingId = await client.CreateBookingAndGetIdAsync(carId, Day(1), Day(5));
        await admin.RejectAsync(bookingId);

        var overview = await admin.GetAsync<ApiClient.AnalyticsOverviewResult>("/api/analytics/overview");

        Assert.Equal(0m, overview!.EstimatedRevenue);
    }

    [Fact]
    public async Task Open_and_critical_issues_are_reflected_in_the_overview()
    {
        var carId = await factory.SeedCarAsync();
        var admin = await SeedAdminAsync();

        await admin.ReportIssueAndGetIdAsync(carId, "Brakes failing", "Critical");
        await admin.ReportIssueAndGetIdAsync(carId, "AC weak", "Low");

        var overview = await admin.GetAsync<ApiClient.AnalyticsOverviewResult>("/api/analytics/overview");

        Assert.Equal(2, overview!.OpenIssueCount);
        Assert.Equal(1, overview.CriticalIssueCount);
    }

    [Fact]
    public async Task Maintenance_cost_sums_service_records_in_range()
    {
        var carId = await factory.SeedCarAsync();
        var admin = await SeedAdminAsync();

        await admin.LogServiceAsync(carId, Day(-1), "Oil change", 45_000, 250m);
        await admin.LogServiceAsync(carId, Day(-2), "Tyre rotation", 45_100, 100m);

        var overview = await admin.GetAsync<ApiClient.AnalyticsOverviewResult>("/api/analytics/overview");

        Assert.Equal(350m, overview!.MaintenanceCost);
    }

    // ---------- Utilization ----------

    [Fact]
    public async Task Utilization_ranks_cars_by_booked_share_of_the_range()
    {
        var busyCar = await factory.SeedCarAsync(name: "Busy Van");
        var idleCar = await factory.SeedCarAsync(name: "Idle Van");
        var admin = await SeedAdminAsync();
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");

        var bookingId = await client.CreateBookingAndGetIdAsync(busyCar, Day(1), Day(10));
        await admin.ApproveAsync(bookingId);

        var utilization = await admin.GetAsync<ApiClient.CarUtilizationResult[]>("/api/analytics/utilization");

        var busy = Array.Find(utilization!, c => c.CarId == busyCar);
        var idle = Array.Find(utilization!, c => c.CarId == idleCar);

        Assert.Equal(10, busy!.BookedDays);
        Assert.Equal(0, idle!.BookedDays);
        Assert.True(busy.UtilizationPercent > idle.UtilizationPercent);
    }

    // ---------- Event type breakdown ----------

    [Fact]
    public async Task Event_type_breakdown_groups_bookings_by_the_events_type()
    {
        var carId = await factory.SeedCarAsync();
        var admin = await SeedAdminAsync();
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");

        await client.CreateBookingAsync(carId, Day(1), Day(3), eventName: "Launch A"); // TradeShow, per ApiClient default
        await client.CreateBookingAsync(carId, Day(5), Day(6), eventName: "Launch B");

        var breakdown = await admin.GetAsync<ApiClient.EventTypeBreakdownResult[]>("/api/analytics/event-types");

        var tradeShow = Array.Find(breakdown!, b => b.EventType == "TradeShow");
        Assert.NotNull(tradeShow);
        Assert.Equal(2, tradeShow!.BookingCount);
    }

    // ---------- Tenant isolation ----------

    [Fact]
    public async Task Analytics_never_mixes_data_across_tenants()
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
        var bookingId = await alphaClient.CreateBookingAndGetIdAsync(alphaCar, Day(1), Day(5));
        await alphaAdmin.ApproveAsync(bookingId);

        var (betaEmail, betaPassword) = await factory.SeedAdminAsync(beta, "admin@beta.com");
        var betaAdmin = factory.CreateTenantClient("beta-motors");
        betaAdmin.Authenticate(await betaAdmin.LoginAsync(betaEmail, betaPassword, asAdmin: true));

        var betaOverview = await betaAdmin.GetAsync<ApiClient.AnalyticsOverviewResult>("/api/analytics/overview");

        Assert.Equal(1, betaOverview!.TotalCars);
        Assert.Equal(0, betaOverview.TotalBookings);
        Assert.Equal(0m, betaOverview.EstimatedRevenue);
    }
}
