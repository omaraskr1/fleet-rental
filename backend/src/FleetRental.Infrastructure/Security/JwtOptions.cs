namespace FleetRental.Infrastructure.Security;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Signing key. Must come from user-secrets, an environment variable, or a
    /// vault — never from a committed appsettings file. Startup refuses to run
    /// with a short or missing key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "FleetRental";

    public string Audience { get; set; } = "FleetRentalApp";

    /// <summary>
    /// Long-lived because Phase 1 has no refresh-token flow, and a client on a
    /// phone should not be logged out mid-booking. Shorten this when refresh
    /// tokens arrive.
    /// </summary>
    public int ExpiryMinutes { get; set; } = 60 * 24 * 7;
}
