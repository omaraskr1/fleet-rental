using FleetRental.Application.Abstractions;
using FleetRental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetRental.Infrastructure.Notifications;

/// <summary>
/// Push channel for booking decisions (feature 6). Resolves the recipient's
/// registered devices and delivers to each.
/// </summary>
/// <remarks>
/// The device-token lookup, payload shape, and per-device fan-out are all real —
/// what is stubbed is the final HTTP call to FCM/APNs, which needs a Firebase
/// service-account key this project does not have yet. Dropping in the real
/// transport means replacing <see cref="DeliverAsync"/> only; nothing that calls
/// this class changes.
/// </remarks>
public class PushNotificationSender(
    Persistence.FleetRentalDbContext db,
    ILogger<PushNotificationSender> logger) : INotificationSender
{
    public string Channel => "Push";

    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        var devices = await db.DeviceTokens
            .AsNoTracking()
            .Where(t => t.UserId == message.RecipientUserId)
            .ToListAsync(cancellationToken);

        if (devices.Count == 0)
        {
            // Normal for a web-only client — not an error worth alerting on.
            logger.LogDebug("No registered devices for user {UserId}; skipping push.", message.RecipientUserId);
            return;
        }

        foreach (var device in devices)
        {
            await DeliverAsync(device, message, cancellationToken);
        }
    }

    /// <summary>
    /// TODO(Phase 1 completion): replace with a real FCM v1 / APNs call once a
    /// Firebase service account exists. Everything above this line is production
    /// shape already.
    /// </summary>
    private Task DeliverAsync(DeviceToken device, NotificationMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[Push not yet wired to FCM] {Platform} device {DeviceId} -> \"{Title}\" (route {Route})",
            device.Platform,
            device.DeviceId,
            message.Subject,
            message.Data.GetValueOrDefault("route", "-"));

        return Task.CompletedTask;
    }
}
