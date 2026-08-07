namespace FleetRental.Infrastructure.Notifications;

public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// When false, emails are written to the log instead of sent. This is the
    /// default so a fresh clone runs without SMTP credentials and a developer
    /// can still watch the approve/reject flow end to end.
    /// </summary>
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public bool UseStartTls { get; set; } = true;

    public string Username { get; set; } = string.Empty;

    /// <summary>Supply via user-secrets or environment, never in appsettings.</summary>
    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = "no-reply@fleetrental.local";

    public string FromName { get; set; } = "Fleet Rental";
}
