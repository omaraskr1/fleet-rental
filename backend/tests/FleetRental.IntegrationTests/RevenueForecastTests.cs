using System.Net;

namespace FleetRental.IntegrationTests;

/// <summary>
/// The revenue forecast endpoint. The SSA math itself is covered by
/// RevenueForecasterTests against synthetic series — this suite only exercises
/// what an HTTP round trip can prove: authorization, and the insufficient-history
/// guard when a tenant has no settled bookings on file.
/// </summary>
/// <remarks>
/// A genuine multi-month history can't be fabricated here: <c>Booking.Request</c>
/// and <c>Reschedule</c> both reject a past start date by domain rule, so there is
/// no way to seed months of "already happened" bookings through the public API —
/// and reaching around that rule with a raw insert would test a scenario the real
/// system can never produce.
/// </remarks>
[Collection(nameof(ApiCollection))]
public class RevenueForecastTests(FleetRentalApiFactory factory) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await factory.ResetAsync();
        await factory.SeedTenantAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_client_cannot_read_the_revenue_forecast()
    {
        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");

        var response = await client.Http.GetAsync("/api/analytics/revenue-forecast");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_read_the_revenue_forecast()
    {
        var anonymous = factory.CreateTenantClient();

        var response = await anonymous.Http.GetAsync("/api/analytics/revenue-forecast");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_tenant_with_no_bookings_reports_insufficient_history_rather_than_a_guess()
    {
        var (email, password) = await factory.SeedAdminAsync();
        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(email, password, asAdmin: true));

        var forecast = await admin.GetAsync<ApiClient.RevenueForecastResult>("/api/analytics/revenue-forecast");

        Assert.False(forecast!.HasSufficientHistory);
        Assert.Empty(forecast.Forecast);
    }

    [Fact]
    public async Task A_tenant_whose_only_bookings_are_this_month_also_reports_insufficient_history()
    {
        // Every booking a fresh fleet can legally create lands in the current or a
        // future month — there is no way to have a "settled" prior month yet.
        var carId = await factory.SeedCarAsync();
        var (email, password) = await factory.SeedAdminAsync();
        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(email, password, asAdmin: true));

        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("client@test.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var bookingId = await client.CreateBookingAndGetIdAsync(carId, today.AddDays(1), today.AddDays(3));
        await admin.ApproveAsync(bookingId);

        var forecast = await admin.GetAsync<ApiClient.RevenueForecastResult>("/api/analytics/revenue-forecast");

        Assert.False(forecast!.HasSufficientHistory);
    }
}
