using System.Net;

namespace FleetRental.IntegrationTests;

/// <summary>
/// Approval likelihood for pending requests, end to end against real data. The
/// model's behaviour on planted patterns is covered by BookingApprovalModelTests
/// against synthetic rows; this suite proves the surrounding pipeline — feature
/// extraction from real bookings, authorization, and tenant isolation.
/// </summary>
[Collection(nameof(ApiCollection))]
public class BookingApprovalPredictionTests(FleetRentalApiFactory factory) : IAsyncLifetime
{
    private const string Endpoint = "/api/analytics/booking-predictions";

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

    /// <summary>
    /// Builds a decision history large enough to clear both guards: approvals on
    /// non-overlapping dates (each one claims its days), and rejections, which
    /// claim nothing and so may overlap freely.
    /// </summary>
    private static async Task SeedDecisionHistoryAsync(
        ApiClient admin, ApiClient client, Guid carId, int approvals, int rejections)
    {
        var cursor = 1;
        for (var i = 0; i < approvals; i++)
        {
            var id = await client.CreateBookingAndGetIdAsync(carId, Day(cursor), Day(cursor + 1));
            await admin.ApproveAsync(id);
            cursor += 3; // leave a gap so the next approval cannot collide
        }

        for (var i = 0; i < rejections; i++)
        {
            var id = await client.CreateBookingAndGetIdAsync(carId, Day(cursor), Day(cursor + 1));
            await admin.RejectAsync(id, "Not available");
        }
    }

    // ---------- Authorization ----------

    [Fact]
    public async Task A_client_cannot_read_approval_predictions()
    {
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");

        var response = await client.Http.GetAsync(Endpoint);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_read_approval_predictions()
    {
        var anonymous = factory.CreateTenantClient();

        var response = await anonymous.Http.GetAsync(Endpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Cold start ----------

    [Fact]
    public async Task A_tenant_with_no_bookings_reports_insufficient_data()
    {
        var admin = await SeedAdminAsync();

        var result = await admin.GetAsync<ApiClient.BookingApprovalPredictionsResult>(Endpoint);

        Assert.False(result!.HasSufficientData);
        Assert.Equal(0, result.TrainedOnBookings);
        Assert.Empty(result.Predictions);
        Assert.True(result.MinimumRequired > 0);
    }

    [Fact]
    public async Task A_handful_of_decisions_is_still_insufficient()
    {
        var carId = await factory.SeedCarAsync();
        var admin = await SeedAdminAsync();
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");

        await SeedDecisionHistoryAsync(admin, client, carId, approvals: 2, rejections: 3);

        var result = await admin.GetAsync<ApiClient.BookingApprovalPredictionsResult>(Endpoint);

        Assert.False(result!.HasSufficientData);
        Assert.Equal(5, result.TrainedOnBookings);
        Assert.Empty(result.Predictions);
    }

    [Fact]
    public async Task A_fleet_that_has_only_ever_approved_reports_insufficient_data()
    {
        // Enough volume, but no rejection to characterise — the endpoint must say
        // so rather than return a model that predicts "yes" with false confidence.
        var carId = await factory.SeedCarAsync();
        var admin = await SeedAdminAsync();
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");

        await SeedDecisionHistoryAsync(admin, client, carId, approvals: 35, rejections: 0);

        var result = await admin.GetAsync<ApiClient.BookingApprovalPredictionsResult>(Endpoint);

        Assert.False(result!.HasSufficientData);
        Assert.Equal(35, result.TrainedOnBookings);
    }

    // ---------- With enough history ----------

    [Fact]
    public async Task Every_pending_request_is_scored_once_enough_decisions_exist()
    {
        var carId = await factory.SeedCarAsync();
        var otherCarId = await factory.SeedCarAsync(name: "Second Car");
        var admin = await SeedAdminAsync();
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");

        await SeedDecisionHistoryAsync(admin, client, carId, approvals: 10, rejections: 25);

        // Two still-undecided requests, on a car the history never touched.
        var pendingA = await client.CreateBookingAndGetIdAsync(otherCarId, Day(200), Day(202));
        var pendingB = await client.CreateBookingAndGetIdAsync(otherCarId, Day(210), Day(215));

        var result = await admin.GetAsync<ApiClient.BookingApprovalPredictionsResult>(Endpoint);

        Assert.True(result!.HasSufficientData);
        Assert.Equal(35, result.TrainedOnBookings);
        Assert.Equal(2, result.Predictions.Length);
        Assert.Contains(result.Predictions, p => p.BookingId == pendingA);
        Assert.Contains(result.Predictions, p => p.BookingId == pendingB);
        Assert.All(result.Predictions, p => Assert.InRange(p.ApprovalProbability, 0d, 1d));
    }

    [Fact]
    public async Task Decided_bookings_are_never_returned_as_predictions()
    {
        // Only the queue needs scoring; a booking already decided has an answer.
        var carId = await factory.SeedCarAsync();
        var admin = await SeedAdminAsync();
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");

        await SeedDecisionHistoryAsync(admin, client, carId, approvals: 10, rejections: 25);

        var result = await admin.GetAsync<ApiClient.BookingApprovalPredictionsResult>(Endpoint);

        Assert.True(result!.HasSufficientData);
        Assert.Empty(result.Predictions);
    }

    // ---------- Tenant isolation ----------

    [Fact]
    public async Task One_tenants_decision_history_never_trains_anothers_model()
    {
        var alpha = await factory.SeedTenantAsync("alpha-rentals");
        var beta = await factory.SeedTenantAsync("beta-motors");

        var alphaCar = await factory.SeedCarAsync(alpha, "Alpha Van");
        var betaCar = await factory.SeedCarAsync(beta, "Beta Truck");

        var (alphaEmail, alphaPassword) = await factory.SeedAdminAsync(alpha, "admin@alpha.com");
        var alphaAdmin = factory.CreateTenantClient("alpha-rentals");
        alphaAdmin.Authenticate(await alphaAdmin.LoginAsync(alphaEmail, alphaPassword, asAdmin: true));

        var alphaClient = factory.CreateTenantClient("alpha-rentals");
        await alphaClient.SignUpAndAuthenticateAsync("client@alpha.com");
        await SeedDecisionHistoryAsync(alphaAdmin, alphaClient, alphaCar, approvals: 10, rejections: 25);

        var (betaEmail, betaPassword) = await factory.SeedAdminAsync(beta, "admin@beta.com");
        var betaAdmin = factory.CreateTenantClient("beta-motors");
        betaAdmin.Authenticate(await betaAdmin.LoginAsync(betaEmail, betaPassword, asAdmin: true));

        var betaClient = factory.CreateTenantClient("beta-motors");
        await betaClient.SignUpAndAuthenticateAsync("client@beta.com");
        await betaClient.CreateBookingAndGetIdAsync(betaCar, Day(5), Day(7));

        var betaResult = await betaAdmin.GetAsync<ApiClient.BookingApprovalPredictionsResult>(Endpoint);

        // Beta has one pending booking and no decisions of its own. If Alpha's 35
        // decisions were visible, Beta would wrongly report a trained model.
        Assert.False(betaResult!.HasSufficientData);
        Assert.Equal(0, betaResult.TrainedOnBookings);
        Assert.Empty(betaResult.Predictions);
    }
}
