using System.Net;

namespace FleetRental.IntegrationTests;

/// <summary>
/// Vehicle maintenance: service history, odometer tracking, and issue reporting.
/// Admin-only end to end — clients booking a car never see any of this.
/// </summary>
[Collection(nameof(ApiCollection))]
public class MaintenanceTests(FleetRentalApiFactory factory) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await factory.ResetAsync();
        await factory.SeedTenantAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(Guid CarId, ApiClient Admin)> SeedCarWithAdminAsync()
    {
        var carId = await factory.SeedCarAsync();
        var (email, password) = await factory.SeedAdminAsync();
        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(email, password, asAdmin: true));
        return (carId, admin);
    }

    // ---------- Authorization ----------

    [Fact]
    public async Task A_client_cannot_log_a_service_record()
    {
        var carId = await factory.SeedCarAsync();
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");

        var response = await client.LogServiceAsync(carId, DateOnly.FromDateTime(DateTime.UtcNow), "Oil change", 1000, 100m);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_client_cannot_report_an_issue()
    {
        var carId = await factory.SeedCarAsync();
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client2@test.com");

        var response = await client.ReportIssueAsync(carId, "AC broken", "Medium");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_read_service_history()
    {
        var carId = await factory.SeedCarAsync();
        var anonymous = factory.CreateTenantClient();

        var response = await anonymous.Http.GetAsync($"/api/cars/{carId}/service-records");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Service records ----------

    [Fact]
    public async Task Logging_a_service_record_appears_in_the_cars_history()
    {
        var (carId, admin) = await SeedCarWithAdminAsync();

        var response = await admin.LogServiceAsync(
            carId, new DateOnly(2026, 1, 15), "Oil and filter change", 45_000, 250m, "Al Futtaim Service");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var history = await admin.GetAsync<ApiClient.ServiceRecordResult[]>($"/api/cars/{carId}/service-records");

        Assert.Single(history!);
        Assert.Equal("Oil and filter change", history![0].Description);
        Assert.Equal(45_000, history[0].OdometerKm);
        Assert.Equal(250m, history[0].Cost);
    }

    [Fact]
    public async Task Service_history_is_newest_first()
    {
        var (carId, admin) = await SeedCarWithAdminAsync();

        await admin.LogServiceAsync(carId, new DateOnly(2026, 1, 1), "First service", 10_000, 100m);
        await admin.LogServiceAsync(carId, new DateOnly(2026, 6, 1), "Second service", 20_000, 150m);

        var history = await admin.GetAsync<ApiClient.ServiceRecordResult[]>($"/api/cars/{carId}/service-records");

        Assert.Equal("Second service", history![0].Description);
        Assert.Equal("First service", history[1].Description);
    }

    [Fact]
    public async Task A_future_service_date_is_rejected()
    {
        var (carId, admin) = await SeedCarWithAdminAsync();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(1);

        var response = await admin.LogServiceAsync(carId, tomorrow, "Service", 1000, 100m);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Logging_a_service_with_a_higher_odometer_reading_updates_the_cars_current_reading()
    {
        var (carId, admin) = await SeedCarWithAdminAsync();

        await admin.LogServiceAsync(carId, new DateOnly(2026, 1, 15), "Service", 45_000, 100m);

        var summary = await admin.GetAsync<ApiClient.MaintenanceSummaryResult>($"/api/cars/{carId}/maintenance");
        Assert.Equal(45_000, summary!.CurrentOdometerKm);
    }

    // ---------- Odometer and service interval ----------

    [Fact]
    public async Task UpdateOdometer_is_reflected_in_the_summary()
    {
        var (carId, admin) = await SeedCarWithAdminAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await admin.UpdateOdometerAsync(carId, 30_000)).StatusCode);

        var summary = await admin.GetAsync<ApiClient.MaintenanceSummaryResult>($"/api/cars/{carId}/maintenance");
        Assert.Equal(30_000, summary!.CurrentOdometerKm);
    }

    [Fact]
    public async Task A_lower_odometer_reading_is_rejected()
    {
        var (carId, admin) = await SeedCarWithAdminAsync();
        await admin.UpdateOdometerAsync(carId, 30_000);

        var response = await admin.UpdateOdometerAsync(carId, 20_000);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_car_with_no_interval_set_is_never_flagged_as_due()
    {
        var (carId, admin) = await SeedCarWithAdminAsync();
        await admin.UpdateOdometerAsync(carId, 500_000); // absurdly high mileage, no interval tracked

        var summary = await admin.GetAsync<ApiClient.MaintenanceSummaryResult>($"/api/cars/{carId}/maintenance");

        Assert.False(summary!.IsServiceDue);
    }

    [Fact]
    public async Task A_car_past_its_interval_since_the_last_service_is_flagged_due()
    {
        var (carId, admin) = await SeedCarWithAdminAsync();

        await admin.SetServiceIntervalAsync(carId, 10_000);
        await admin.LogServiceAsync(carId, new DateOnly(2026, 1, 1), "Last service", 10_000, 100m);
        await admin.UpdateOdometerAsync(carId, 21_000); // 11,000 km since last service

        var summary = await admin.GetAsync<ApiClient.MaintenanceSummaryResult>($"/api/cars/{carId}/maintenance");

        Assert.True(summary!.IsServiceDue);
        Assert.Equal(11_000, summary.KmSinceLastService);
    }

    [Fact]
    public async Task A_car_exactly_at_its_interval_is_flagged_due()
    {
        // The boundary case: reaching the interval exactly should trip the flag,
        // not require going a km past it — "due at 10,000 km" means due AT
        // 10,000, not at 10,001.
        var (carId, admin) = await SeedCarWithAdminAsync();

        await admin.SetServiceIntervalAsync(carId, 10_000);
        await admin.LogServiceAsync(carId, new DateOnly(2026, 1, 1), "Last service", 10_000, 100m);
        await admin.UpdateOdometerAsync(carId, 20_000); // exactly 10,000 km since

        var summary = await admin.GetAsync<ApiClient.MaintenanceSummaryResult>($"/api/cars/{carId}/maintenance");

        Assert.True(summary!.IsServiceDue);
        Assert.Equal(10_000, summary.KmSinceLastService);
    }

    [Fact]
    public async Task A_car_within_its_interval_is_not_flagged_due()
    {
        var (carId, admin) = await SeedCarWithAdminAsync();

        await admin.SetServiceIntervalAsync(carId, 10_000);
        await admin.LogServiceAsync(carId, new DateOnly(2026, 1, 1), "Last service", 10_000, 100m);
        await admin.UpdateOdometerAsync(carId, 15_000); // only 5,000 km since

        var summary = await admin.GetAsync<ApiClient.MaintenanceSummaryResult>($"/api/cars/{carId}/maintenance");

        Assert.False(summary!.IsServiceDue);
    }

    // ---------- Issues ----------

    [Fact]
    public async Task Reporting_an_issue_appears_in_the_fleet_wide_list()
    {
        var (carId, admin) = await SeedCarWithAdminAsync();

        var issueId = await admin.ReportIssueAndGetIdAsync(carId, "AC not cooling", "Medium");

        var issues = await admin.GetAsync<ApiClient.VehicleIssueResult[]>("/api/issues");

        Assert.Contains(issues!, i => i.Id == issueId && i.Description == "AC not cooling");
    }

    [Fact]
    public async Task Issues_can_be_filtered_to_one_car()
    {
        var (carId, admin) = await SeedCarWithAdminAsync();
        var otherCarId = await factory.SeedCarAsync(name: "Other Car");

        await admin.ReportIssueAndGetIdAsync(carId, "Issue on car A", "Low");
        await admin.ReportIssueAndGetIdAsync(otherCarId, "Issue on car B", "Low");

        var issues = await admin.GetAsync<ApiClient.VehicleIssueResult[]>($"/api/issues?carId={carId}");

        Assert.Single(issues!);
        Assert.Equal("Issue on car A", issues![0].Description);
    }

    [Fact]
    public async Task A_critical_open_issue_marks_the_car_as_having_a_blocking_issue()
    {
        var (carId, admin) = await SeedCarWithAdminAsync();
        await admin.ReportIssueAndGetIdAsync(carId, "Brakes failing", "Critical");

        var summary = await admin.GetAsync<ApiClient.MaintenanceSummaryResult>($"/api/cars/{carId}/maintenance");

        Assert.True(summary!.HasBlockingIssue);
        Assert.Equal(1, summary.OpenIssueCount);
    }

    [Fact]
    public async Task Resolving_the_only_critical_issue_clears_the_blocking_flag()
    {
        var (carId, admin) = await SeedCarWithAdminAsync();
        var issueId = await admin.ReportIssueAndGetIdAsync(carId, "Brakes failing", "Critical");

        await admin.ResolveIssueAsync(issueId, "Replaced brake pads");

        var summary = await admin.GetAsync<ApiClient.MaintenanceSummaryResult>($"/api/cars/{carId}/maintenance");

        Assert.False(summary!.HasBlockingIssue);
        Assert.Equal(0, summary.OpenIssueCount);
    }

    [Fact]
    public async Task Issue_lifecycle_open_to_in_progress_to_resolved()
    {
        var (carId, admin) = await SeedCarWithAdminAsync();
        var issueId = await admin.ReportIssueAndGetIdAsync(carId, "Squeaky brakes", "Low");

        Assert.Equal(HttpStatusCode.OK, (await admin.StartIssueProgressAsync(issueId)).StatusCode);

        var midway = await admin.GetAsync<ApiClient.VehicleIssueResult[]>($"/api/issues?carId={carId}");
        Assert.Equal("InProgress", midway![0].Status);

        Assert.Equal(HttpStatusCode.OK, (await admin.ResolveIssueAsync(issueId, "Fixed")).StatusCode);

        var resolved = await admin.GetAsync<ApiClient.VehicleIssueResult[]>($"/api/issues?status=Resolved");
        Assert.Contains(resolved!, i => i.Id == issueId);
    }

    [Fact]
    public async Task Reopening_a_resolved_issue_puts_it_back_in_the_open_list()
    {
        var (carId, admin) = await SeedCarWithAdminAsync();
        var issueId = await admin.ReportIssueAndGetIdAsync(carId, "Rattling noise", "Low");
        await admin.ResolveIssueAsync(issueId);

        Assert.Equal(HttpStatusCode.OK, (await admin.ReopenIssueAsync(issueId)).StatusCode);

        var open = await admin.GetAsync<ApiClient.VehicleIssueResult[]>("/api/issues?status=Open");
        Assert.Contains(open!, i => i.Id == issueId);
    }

    [Fact]
    public async Task Every_maintenance_row_is_scoped_to_its_own_tenant()
    {
        var alpha = await factory.SeedTenantAsync("alpha-rentals");
        var beta = await factory.SeedTenantAsync("beta-motors");

        var alphaCar = await factory.SeedCarAsync(alpha, "Alpha Van");
        var betaCar = await factory.SeedCarAsync(beta, "Beta Truck");

        var (alphaEmail, alphaPassword) = await factory.SeedAdminAsync(alpha, "admin@alpha.com");
        var alphaAdmin = factory.CreateTenantClient("alpha-rentals");
        alphaAdmin.Authenticate(await alphaAdmin.LoginAsync(alphaEmail, alphaPassword, asAdmin: true));

        var (betaEmail, betaPassword) = await factory.SeedAdminAsync(beta, "admin@beta.com");
        var betaAdmin = factory.CreateTenantClient("beta-motors");
        betaAdmin.Authenticate(await betaAdmin.LoginAsync(betaEmail, betaPassword, asAdmin: true));

        await alphaAdmin.ReportIssueAndGetIdAsync(alphaCar, "Alpha issue", "Low");
        await betaAdmin.ReportIssueAndGetIdAsync(betaCar, "Beta issue", "Low");

        var alphaIssues = await alphaAdmin.GetAsync<ApiClient.VehicleIssueResult[]>("/api/issues");
        var betaIssues = await betaAdmin.GetAsync<ApiClient.VehicleIssueResult[]>("/api/issues");

        Assert.Single(alphaIssues!);
        Assert.Single(betaIssues!);
        Assert.Equal("Alpha issue", alphaIssues![0].Description);
        Assert.Equal("Beta issue", betaIssues![0].Description);
    }

    [Fact]
    public async Task An_admin_cannot_reach_another_tenants_car_by_id()
    {
        var alpha = await factory.SeedTenantAsync("alpha-rentals");
        var beta = await factory.SeedTenantAsync("beta-motors");
        var betaCar = await factory.SeedCarAsync(beta, "Beta Truck");

        var (alphaEmail, alphaPassword) = await factory.SeedAdminAsync(alpha, "admin@alpha.com");
        var alphaAdmin = factory.CreateTenantClient("alpha-rentals");
        alphaAdmin.Authenticate(await alphaAdmin.LoginAsync(alphaEmail, alphaPassword, asAdmin: true));

        var response = await alphaAdmin.LogServiceAsync(betaCar, DateOnly.FromDateTime(DateTime.UtcNow), "Service", 1000, 100m);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
