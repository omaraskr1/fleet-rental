namespace FleetRental.Application.Abstractions;

/// <summary>
/// One channel for delivering a notification.
/// </summary>
/// <remarks>
/// Phase 1 ships email and push implementations. Phase 2's WhatsApp channel is a
/// third class implementing this interface and one DI registration — no caller
/// changes, which is the whole point of dispatching through a collection of these
/// rather than calling an email client directly.
/// </remarks>
public interface INotificationSender
{
    /// <summary>Channel name, used for logging and for opting channels out per user.</summary>
    string Channel { get; }

    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// A channel-agnostic notification. Each sender renders the parts it can use —
/// email takes subject and body, push takes title and body, WhatsApp will take
/// body plus <see cref="Data"/>.
/// </summary>
public sealed record NotificationMessage
{
    public required Guid RecipientUserId { get; init; }

    public required string RecipientEmail { get; init; }

    public string? RecipientPhone { get; init; }

    public required string Subject { get; init; }

    public required string Body { get; init; }

    /// <summary>Structured payload for deep-linking the mobile app to the booking.</summary>
    public IReadOnlyDictionary<string, string> Data { get; init; } =
        new Dictionary<string, string>();
}
