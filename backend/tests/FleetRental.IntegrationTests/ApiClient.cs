using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FleetRental.IntegrationTests;

/// <summary>
/// Small typed helper over HttpClient so tests read as scenarios rather than as
/// serialisation plumbing.
/// </summary>
public class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public HttpClient Http => http;

    public void Authenticate(string token) =>
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public void SignOut() => http.DefaultRequestHeaders.Authorization = null;

    public async Task<string> SignUpAsync(string email, string password = "ClientPass123", string name = "Test Client")
    {
        var response = await http.PostAsJsonAsync("/api/auth/signup",
            new { email, password, fullName = name }, Json);

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResult>(Json))!.AccessToken;
    }

    public async Task<string> LoginAsync(string email, string password, bool asAdmin = false)
    {
        var route = asAdmin ? "/api/auth/admin/login" : "/api/auth/login";
        var response = await http.PostAsJsonAsync(route, new { email, password }, Json);

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResult>(Json))!.AccessToken;
    }

    /// <summary>Signs up a client and authenticates this client instance as them.</summary>
    public async Task<string> SignUpAndAuthenticateAsync(string email)
    {
        var token = await SignUpAsync(email);
        Authenticate(token);
        return token;
    }

    public Task<HttpResponseMessage> CreateBookingAsync(
        Guid carId,
        DateOnly start,
        DateOnly end,
        string eventName = "Test Event",
        string location = "Test Location") =>
        http.PostAsJsonAsync("/api/bookings", new
        {
            carId,
            startDate = start.ToString("yyyy-MM-dd"),
            endDate = end.ToString("yyyy-MM-dd"),
            eventName,
            eventType = "TradeShow",
            eventLocation = location,
        }, Json);

    public async Task<Guid> CreateBookingAndGetIdAsync(Guid carId, DateOnly start, DateOnly end)
    {
        var response = await CreateBookingAsync(carId, start, end);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BookingResult>(Json))!.Id;
    }

    public Task<HttpResponseMessage> ApproveAsync(Guid bookingId, string? reason = null) =>
        http.PostAsJsonAsync($"/api/bookings/{bookingId}/approve", new { reason }, Json);

    public Task<HttpResponseMessage> RejectAsync(Guid bookingId, string? reason = null) =>
        http.PostAsJsonAsync($"/api/bookings/{bookingId}/reject", new { reason }, Json);

    public Task<HttpResponseMessage> CancelAsync(Guid bookingId) =>
        http.PostAsJsonAsync($"/api/bookings/{bookingId}/cancel", new { }, Json);

    public async Task<T?> GetAsync<T>(string url) =>
        await http.GetFromJsonAsync<T>(url, Json);

    // ---------- Maintenance ----------

    public Task<HttpResponseMessage> LogServiceAsync(
        Guid carId,
        DateOnly performedAt,
        string description,
        int? odometerKm,
        decimal cost,
        string? performedBy = null) =>
        http.PostAsJsonAsync($"/api/cars/{carId}/service-records", new
        {
            performedAt = performedAt.ToString("yyyy-MM-dd"),
            description,
            odometerKm,
            cost,
            performedBy,
        }, Json);

    public Task<HttpResponseMessage> UpdateOdometerAsync(Guid carId, int km) =>
        http.PutAsJsonAsync($"/api/cars/{carId}/odometer", new { km }, Json);

    public Task<HttpResponseMessage> SetServiceIntervalAsync(Guid carId, int? km) =>
        http.PutAsJsonAsync($"/api/cars/{carId}/service-interval", new { km }, Json);

    public Task<HttpResponseMessage> ReportIssueAsync(Guid carId, string description, string severity) =>
        http.PostAsJsonAsync($"/api/cars/{carId}/issues", new { description, severity }, Json);

    public async Task<Guid> ReportIssueAndGetIdAsync(Guid carId, string description, string severity)
    {
        var response = await ReportIssueAsync(carId, description, severity);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VehicleIssueResult>(Json))!.Id;
    }

    public Task<HttpResponseMessage> ResolveIssueAsync(Guid issueId, string? resolutionNotes = null) =>
        http.PostAsJsonAsync($"/api/issues/{issueId}/resolve", new { resolutionNotes }, Json);

    public Task<HttpResponseMessage> StartIssueProgressAsync(Guid issueId) =>
        http.PostAsJsonAsync($"/api/issues/{issueId}/start-progress", new { }, Json);

    public Task<HttpResponseMessage> ReopenIssueAsync(Guid issueId) =>
        http.PostAsJsonAsync($"/api/issues/{issueId}/reopen", new { }, Json);

    public record VehicleIssueResult(
        Guid Id, Guid CarId, string CarName, string ReportedByName,
        string Description, string Severity, string Status);

    public record ServiceRecordResult(Guid Id, Guid CarId, string PerformedAt, string Description, int? OdometerKm, decimal Cost);

    public record MaintenanceSummaryResult(
        Guid CarId, string CarName, int? CurrentOdometerKm, int? ServiceIntervalKm,
        string? LastServiceAt, int? KmSinceLastService, bool IsServiceDue,
        int OpenIssueCount, bool HasBlockingIssue);

    public record AuthResult(string AccessToken, DateTimeOffset ExpiresAt, UserResult User);

    public record UserResult(Guid Id, string Email, string FullName, string Role);

    public record BookingResult(Guid Id, Guid CarId, string Status, int TotalDays, string StartDate, string EndDate);

    public record AvailabilityResult(Guid CarId, string[] BookedDates, string[] PendingDates, bool CarIsBookable);

    public record ProblemResult(int Status, string Title, string Detail);

    // ---------- Analytics ----------

    public record AnalyticsOverviewResult(
        string From, string To, int TotalCars, int ActiveCars, int TotalBookings,
        int PendingBookings, int ApprovedBookings, int RejectedBookings, int CancelledBookings,
        double ApprovalRatePercent, decimal EstimatedRevenue, double FleetUtilizationPercent,
        int OpenIssueCount, int CriticalIssueCount, int CarsServiceDue, decimal MaintenanceCost);

    public record RevenuePointResult(string PeriodLabel, string PeriodStart, decimal EstimatedRevenue, int ApprovedBookings);

    public record CarUtilizationResult(
        Guid CarId, string CarName, int BookedDays, int DaysInRange,
        double UtilizationPercent, int BookingCount, decimal EstimatedRevenue);

    public record EventTypeBreakdownResult(string EventType, int BookingCount, int ApprovedCount, decimal EstimatedRevenue);

    public record MaintenanceCostPointResult(string PeriodLabel, string PeriodStart, decimal TotalCost, int RecordCount);

    public record DemandPointResult(string PeriodLabel, string PeriodStart, double BookedDays);

    public record CategoryDemandResult(
        string Category, int CarCount, bool HasSufficientHistory,
        DemandPointResult[] History, DemandPointResult[] Forecast,
        string Trend, double RecentMonthlyAverage, double ForecastMonthlyAverage);

    public record CarProfitabilityResult(
        Guid CarId, string CarName, decimal EstimatedRevenue, decimal MaintenanceCost,
        decimal NetProfit, double? ProfitMarginPercent, double UtilizationPercent,
        int BookingCount, string Recommendation);

    public record RevenueForecastPointResult(string PeriodLabel, string PeriodStart, decimal ForecastedRevenue, decimal LowerBound, decimal UpperBound);

    public record RevenueForecastResult(
        bool HasSufficientHistory, RevenuePointResult[] History, RevenueForecastPointResult[] Forecast);
}
