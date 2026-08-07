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

    public record AuthResult(string AccessToken, DateTimeOffset ExpiresAt, UserResult User);

    public record UserResult(Guid Id, string Email, string FullName, string Role);

    public record BookingResult(Guid Id, Guid CarId, string Status, int TotalDays, string StartDate, string EndDate);

    public record AvailabilityResult(Guid CarId, string[] BookedDates, string[] PendingDates, bool CarIsBookable);

    public record ProblemResult(int Status, string Title, string Detail);
}
