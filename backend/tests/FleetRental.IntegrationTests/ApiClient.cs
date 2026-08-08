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
        string? performedBy = null,
        Guid? serviceTypeId = null) =>
        http.PostAsJsonAsync($"/api/cars/{carId}/service-records", new
        {
            performedAt = performedAt.ToString("yyyy-MM-dd"),
            description,
            odometerKm,
            cost,
            performedBy,
            serviceTypeId,
        }, Json);

    public Task<HttpResponseMessage> UpdateOdometerAsync(Guid carId, int km) =>
        http.PutAsJsonAsync($"/api/cars/{carId}/odometer", new { km }, Json);

    public Task<HttpResponseMessage> SetServiceIntervalAsync(Guid carId, int? km) =>
        http.PutAsJsonAsync($"/api/cars/{carId}/service-interval", new { km }, Json);

    // ---------- Service catalog ----------

    public Task<HttpResponseMessage> CreateServiceTypeAsync(string name, int intervalKm) =>
        http.PostAsJsonAsync("/api/service-types", new { name, intervalKm }, Json);

    public async Task<Guid> CreateServiceTypeAndGetIdAsync(string name, int intervalKm)
    {
        var response = await CreateServiceTypeAsync(name, intervalKm);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ServiceTypeResult>(Json))!.Id;
    }

    public Task<HttpResponseMessage> GetServiceTypesAsync(bool includeInactive = false) =>
        http.GetAsync($"/api/service-types?includeInactive={includeInactive}");

    public Task<HttpResponseMessage> DeactivateServiceTypeAsync(Guid serviceTypeId) =>
        http.PostAsJsonAsync($"/api/service-types/{serviceTypeId}/deactivate", new { }, Json);

    public Task<HttpResponseMessage> ReactivateServiceTypeAsync(Guid serviceTypeId) =>
        http.PostAsJsonAsync($"/api/service-types/{serviceTypeId}/reactivate", new { }, Json);

    public Task<HttpResponseMessage> GetServiceTypeStatusAsync(Guid carId) =>
        http.GetAsync($"/api/cars/{carId}/service-type-status");

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

    public record ServiceRecordResult(
        Guid Id, Guid CarId, string PerformedAt, string Description, int? OdometerKm, decimal Cost,
        Guid? ServiceTypeId, string? ServiceTypeName);

    public record ServiceTypeResult(Guid Id, string Name, int IntervalKm, bool IsActive);

    public record ServiceTypeStatusResult(
        Guid ServiceTypeId, string ServiceTypeName, int IntervalKm,
        string? LastPerformedAt, int? KmSinceLastService, bool IsDue);

    public record MaintenanceSummaryResult(
        Guid CarId, string CarName, int? CurrentOdometerKm, int? ServiceIntervalKm,
        string? LastServiceAt, int? KmSinceLastService, bool IsServiceDue,
        int OpenIssueCount, bool HasBlockingIssue);

    public record AuthResult(string AccessToken, DateTimeOffset ExpiresAt, UserResult User);

    public record UserResult(Guid Id, string Email, string FullName, string Role);

    public record BookingResult(Guid Id, Guid CarId, string Status, int TotalDays, string StartDate, string EndDate);

    public record AvailabilityResult(Guid CarId, string[] BookedDates, string[] PendingDates, bool CarIsBookable);

    public record ProblemResult(int Status, string Title, string Detail);

    // ---------- Platform ----------

    public async Task<string> PlatformLoginAsync(string email, string password)
    {
        var response = await http.PostAsJsonAsync("/api/platform/auth/login", new { email, password }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PlatformAuthResult>(Json))!.AccessToken;
    }

    public Task<HttpResponseMessage> CreateCompanyAsync(string name, string code, string? contactEmail = null) =>
        http.PostAsJsonAsync("/api/platform/companies", new { name, code, contactEmail }, Json);

    public async Task<Guid> CreateCompanyAndGetIdAsync(string name, string code)
    {
        var response = await CreateCompanyAsync(name, code);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CompanyResult>(Json))!.Id;
    }

    public Task<HttpResponseMessage> GetCompaniesAsync() => http.GetAsync("/api/platform/companies");

    public Task<HttpResponseMessage> SuspendCompanyAsync(Guid tenantId) =>
        http.PostAsJsonAsync($"/api/platform/companies/{tenantId}/suspend", new { }, Json);

    public Task<HttpResponseMessage> ReactivateCompanyAsync(Guid tenantId) =>
        http.PostAsJsonAsync($"/api/platform/companies/{tenantId}/reactivate", new { }, Json);

    public Task<HttpResponseMessage> CreateCompanyAdminAsync(
        Guid tenantId, string email, string password, string fullName = "Company Admin") =>
        http.PostAsJsonAsync($"/api/platform/companies/{tenantId}/admins", new { email, password, fullName }, Json);

    public Task<HttpResponseMessage> GetCompanyAdminsAsync(Guid tenantId) =>
        http.GetAsync($"/api/platform/companies/{tenantId}/admins");

    public Task<HttpResponseMessage> CreatePlatformCarAsync(
        Guid companyId, string name, string category = "Sedan", int seats = 4, decimal rate = 100m) =>
        http.PostAsJsonAsync("/api/platform/cars", new { companyId, name, category, seats, rate }, Json);

    public Task<HttpResponseMessage> GetPlatformCarsAsync() => http.GetAsync("/api/platform/cars");

    public Task<HttpResponseMessage> CreatePlatformAdminAsync(string email, string password, string fullName) =>
        http.PostAsJsonAsync("/api/platform/admins", new { email, password, fullName }, Json);

    public record PlatformAuthResult(string AccessToken, DateTimeOffset ExpiresAt, PlatformAdminResult Admin);

    public record PlatformAdminResult(Guid Id, string Email, string FullName, bool IsActive);

    public record CompanyResult(Guid Id, string Name, string Code, string? ContactEmail, string Status);

    public record CompanyAdminResult(Guid Id, string Email, string FullName, bool IsActive);

    public record PlatformCarResult(
        Guid Id, Guid CompanyId, string CompanyName, string Name, string Description,
        string Category, int Seats, decimal Rate, string PricingModel, string Status, string? LicensePlate);

    // ---------- GPS ----------

    public Task<HttpResponseMessage> ReportLocationAsync(
        string deviceKey, double latitude, double longitude, DateTimeOffset? recordedAt = null) =>
        http.PostAsJsonAsync("/api/gps/locations", new { deviceKey, latitude, longitude, recordedAt }, Json);

    public Task<HttpResponseMessage> GetCarLocationsAsync() => http.GetAsync("/api/cars/locations");

    public Task<HttpResponseMessage> GetGpsDeviceKeyAsync(Guid carId) =>
        http.GetAsync($"/api/cars/{carId}/gps-device-key");

    public async Task<string> RegenerateGpsDeviceKeyAndGetAsync(Guid carId)
    {
        var response = await http.PostAsync($"/api/cars/{carId}/gps-device-key/regenerate", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GpsDeviceKeyResult>(Json))!.DeviceKey!;
    }

    public record GpsDeviceKeyResult(Guid CarId, string? DeviceKey);

    public record CarLocationResult(Guid CarId, string CarName, double Latitude, double Longitude, DateTimeOffset RecordedAt);

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

    public record BookingApprovalPredictionResult(Guid BookingId, double ApprovalProbability);

    public record BookingApprovalPredictionsResult(
        bool HasSufficientData, int TrainedOnBookings, int MinimumRequired,
        BookingApprovalPredictionResult[] Predictions);

    public record RevenueForecastPointResult(string PeriodLabel, string PeriodStart, decimal ForecastedRevenue, decimal LowerBound, decimal UpperBound);

    public record RevenueForecastResult(
        bool HasSufficientHistory, RevenuePointResult[] History, RevenueForecastPointResult[] Forecast);
}
