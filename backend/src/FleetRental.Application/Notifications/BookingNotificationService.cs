using FleetRental.Application.Abstractions;
using FleetRental.Application.Platform;
using FleetRental.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetRental.Application.Notifications;

/// <summary>
/// Builds the approve/reject notification (feature 6) and fans it out across every
/// registered channel.
/// </summary>
/// <remarks>
/// Adding WhatsApp in Phase 2 means writing one <see cref="INotificationSender"/>
/// and registering it — nothing here changes.
/// </remarks>
public class BookingNotificationService(
    IFleetRentalDbContext db,
    IEnumerable<INotificationSender> senders,
    TenantFeatureGate features,
    ILogger<BookingNotificationService> logger)
{
    public async Task NotifyDecisionAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await db.Bookings
            .Include(b => b.Car)
            .Include(b => b.Client)
            .Include(b => b.Event)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking is null)
        {
            logger.LogWarning("Cannot notify for booking {BookingId}: not found.", bookingId);
            return;
        }

        if (booking.Status is not (BookingStatus.Approved or BookingStatus.Rejected))
        {
            return;
        }

        // Guards against a retry or a restarted worker notifying the client twice.
        if (booking.NotifiedAt is not null)
        {
            logger.LogDebug("Booking {BookingId} was already notified at {NotifiedAt}.", bookingId, booking.NotifiedAt);
            return;
        }

        var approved = booking.Status == BookingStatus.Approved;
        var dates = $"{booking.Period.Start:d MMM yyyy} – {booking.Period.End:d MMM yyyy}";

        var message = new NotificationMessage
        {
            RecipientUserId = booking.ClientId,
            RecipientEmail = booking.Client.Email,
            RecipientPhone = booking.Client.PhoneNumber,
            Subject = approved
                ? $"Your booking for {booking.Car.Name} is confirmed"
                : $"Your booking request for {booking.Car.Name} was declined",
            Body = BuildBody(booking.Client.FullName, booking.Car.Name, booking.Event.Name, dates, approved, booking.DecisionReason),
            Data = new Dictionary<string, string>
            {
                ["bookingId"] = booking.Id.ToString(),
                ["carId"] = booking.CarId.ToString(),
                ["status"] = booking.Status.ToString(),
                // Lets the mobile app deep-link straight to the booking on tap.
                ["route"] = $"/bookings/{booking.Id}",
            },
        };

        var pushEnabled = await features.IsEnabledForAsync(booking.TenantId, FeatureKey.PushNotifications, cancellationToken);
        var delivered = false;

        foreach (var sender in senders)
        {
            // Email is never gated — only push is a per-company toggle.
            if (sender.Channel == "Push" && !pushEnabled)
            {
                continue;
            }

            try
            {
                await sender.SendAsync(message, cancellationToken);
                delivered = true;
                logger.LogInformation(
                    "Sent {Status} notification for booking {BookingId} via {Channel}.",
                    booking.Status, booking.Id, sender.Channel);
            }
            catch (Exception ex)
            {
                // One dead channel must not stop the others or fail the approval —
                // the booking decision is already committed and is what matters.
                logger.LogError(ex,
                    "Failed to send booking {BookingId} notification via {Channel}.",
                    booking.Id, sender.Channel);
            }
        }

        if (delivered)
        {
            booking.MarkNotified();
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string BuildBody(
        string clientName,
        string carName,
        string eventName,
        string dates,
        bool approved,
        string? reason)
    {
        var reasonLine = string.IsNullOrWhiteSpace(reason)
            ? string.Empty
            : $"\n\nNote from the fleet team: {reason}";

        return approved
            ? $"""
               Hi {clientName},

               Good news — your booking is confirmed.

               Vehicle: {carName}
               Event:   {eventName}
               Dates:   {dates}

               We'll be in touch closer to the date with pickup details.{reasonLine}

               — Fleet Rental
               """
            : $"""
               Hi {clientName},

               Unfortunately we can't fulfil your booking request.

               Vehicle: {carName}
               Event:   {eventName}
               Dates:   {dates}{reasonLine}

               You're welcome to submit another request for different dates.

               — Fleet Rental
               """;
    }
}
