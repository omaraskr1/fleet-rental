using FleetRental.Domain.Common;
using FleetRental.Domain.Enums;
using FleetRental.Domain.ValueObjects;

namespace FleetRental.Domain.Entities;

/// <summary>
/// A client's request for one car over one date range, for one event.
/// Aggregate root — every status change goes through the methods here so the
/// transition rules live in exactly one place.
/// </summary>
public class Booking : Entity
{
    private readonly List<BookedDay> _bookedDays = [];

    private Booking() { } // EF Core

    private Booking(Guid carId, Guid clientId, Guid eventId, DateRange period, string? clientNotes)
    {
        CarId = carId;
        ClientId = clientId;
        EventId = eventId;
        Period = period;
        ClientNotes = clientNotes;
        Status = BookingStatus.Pending;
    }

    public Guid CarId { get; private set; }

    public Car Car { get; private set; } = null!;

    public Guid ClientId { get; private set; }

    public User Client { get; private set; } = null!;

    public Guid EventId { get; private set; }

    public Event Event { get; private set; } = null!;

    /// <summary>Requested rental days, inclusive of both endpoints.</summary>
    public DateRange Period { get; private set; }

    public BookingStatus Status { get; private set; }

    public string? ClientNotes { get; private set; }

    /// <summary>When an admin approved or rejected. Null while pending.</summary>
    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Which admin decided. Kept for the Phase 3 multi-admin audit trail.</summary>
    public Guid? DecidedByUserId { get; private set; }

    /// <summary>Admin's reason, surfaced to the client in the rejection notification.</summary>
    public string? DecisionReason { get; private set; }

    /// <summary>
    /// Set once the approve/reject notification has actually been dispatched, so a
    /// retry or a restarted worker does not notify the client twice.
    /// </summary>
    public DateTimeOffset? NotifiedAt { get; private set; }

    /// <summary>
    /// The individual days this booking holds. Non-empty only while approved; the
    /// unique index over these rows is what actually prevents double-booking.
    /// </summary>
    public IReadOnlyCollection<BookedDay> BookedDays => _bookedDays.AsReadOnly();

    /// <summary>Only approved bookings hold dates against other clients.</summary>
    public bool BlocksDates => Status == BookingStatus.Approved;

    /// <summary>True while the request can still be approved, rejected, or cancelled.</summary>
    public bool IsOpen => Status == BookingStatus.Pending;

    public static Booking Request(
        Car car,
        Guid clientId,
        Guid eventId,
        DateRange period,
        string? clientNotes = null)
    {
        ArgumentNullException.ThrowIfNull(car);

        if (!car.IsBookable)
        {
            throw new DomainException($"'{car.Name}' is not currently accepting bookings.");
        }

        // Rejecting past dates here rather than in a validator keeps the rule with
        // the data, so it holds for any caller including seeding and admin tools.
        if (period.Start < DateOnly.FromDateTime(DateTime.UtcNow.Date))
        {
            throw new DomainException("Booking start date cannot be in the past.");
        }

        return new Booking(car.Id, clientId, eventId, period, clientNotes?.Trim());
    }

    /// <summary>
    /// Approves the request. The caller is responsible for having verified
    /// availability inside the same transaction — this method guards the state
    /// transition, not the overlap.
    /// </summary>
    public void Approve(Guid decidedByUserId, string? reason = null)
    {
        if (Status != BookingStatus.Pending)
        {
            throw new DomainException($"Only pending bookings can be approved; this one is {Status}.");
        }

        Status = BookingStatus.Approved;
        DecidedAt = DateTimeOffset.UtcNow;
        DecidedByUserId = decidedByUserId;
        DecisionReason = reason?.Trim();

        // Claim each day. If a competing booking already holds one of them, the
        // unique index on (CarId, Date) rejects the INSERT and this whole
        // transaction rolls back — which is exactly the outcome we want.
        _bookedDays.Clear();
        foreach (var day in Period.EnumerateDays())
        {
            _bookedDays.Add(new BookedDay(CarId, Id, day));
        }

        Touch();
    }

    public void Reject(Guid decidedByUserId, string? reason = null)
    {
        if (Status != BookingStatus.Pending)
        {
            throw new DomainException($"Only pending bookings can be rejected; this one is {Status}.");
        }

        Status = BookingStatus.Rejected;
        DecidedAt = DateTimeOffset.UtcNow;
        DecidedByUserId = decidedByUserId;
        DecisionReason = reason?.Trim();
        Touch();
    }

    /// <summary>
    /// Client withdraws the request. Allowed while pending, and also after approval
    /// so a client can free up dates they no longer need.
    /// </summary>
    public void Cancel()
    {
        if (Status is BookingStatus.Rejected or BookingStatus.Cancelled)
        {
            throw new DomainException($"A {Status} booking cannot be cancelled.");
        }

        Status = BookingStatus.Cancelled;

        // Release the held days so the dates open up again for other clients.
        _bookedDays.Clear();

        Touch();
    }

    /// <summary>
    /// Lets a client adjust dates while the request is still pending, rather than
    /// cancelling and re-submitting.
    /// </summary>
    public void Reschedule(DateRange period)
    {
        if (Status != BookingStatus.Pending)
        {
            throw new DomainException($"Only pending bookings can be rescheduled; this one is {Status}.");
        }

        if (period.Start < DateOnly.FromDateTime(DateTime.UtcNow.Date))
        {
            throw new DomainException("Booking start date cannot be in the past.");
        }

        Period = period;
        Touch();
    }

    public void MarkNotified()
    {
        NotifiedAt = DateTimeOffset.UtcNow;
        Touch();
    }
}
