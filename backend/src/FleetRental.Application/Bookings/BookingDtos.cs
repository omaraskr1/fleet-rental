using FleetRental.Domain.Entities;
using FleetRental.Domain.Enums;

namespace FleetRental.Application.Bookings;

public sealed record BookingDto
{
    public required Guid Id { get; init; }

    public required Guid CarId { get; init; }

    public required string CarName { get; init; }

    public string? CarPhotoUrl { get; init; }

    public required Guid ClientId { get; init; }

    public required string ClientName { get; init; }

    public required string ClientEmail { get; init; }

    public required DateOnly StartDate { get; init; }

    public required DateOnly EndDate { get; init; }

    public required int TotalDays { get; init; }

    public required string Status { get; init; }

    public string? ClientNotes { get; init; }

    public required EventSummaryDto Event { get; init; }

    public DateTimeOffset? DecidedAt { get; init; }

    public string? DecisionReason { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Requires Car, Client, and Event to be loaded. Callers use
    /// <c>BookingQueries.WithDetails</c> so this never silently lazy-loads.
    /// </summary>
    public static BookingDto FromEntity(Booking booking) => new()
    {
        Id = booking.Id,
        CarId = booking.CarId,
        CarName = booking.Car.Name,
        CarPhotoUrl = booking.Car.PrimaryPhoto?.Url,
        ClientId = booking.ClientId,
        ClientName = booking.Client.FullName,
        ClientEmail = booking.Client.Email,
        StartDate = booking.Period.Start,
        EndDate = booking.Period.End,
        TotalDays = booking.Period.TotalDays,
        Status = booking.Status.ToString(),
        ClientNotes = booking.ClientNotes,
        Event = EventSummaryDto.FromEntity(booking.Event),
        DecidedAt = booking.DecidedAt,
        DecisionReason = booking.DecisionReason,
        CreatedAt = booking.CreatedAt,
    };
}

public sealed record EventSummaryDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Type { get; init; }

    public required string Location { get; init; }

    public int? ExpectedAttendance { get; init; }

    public string? Notes { get; init; }

    public static EventSummaryDto FromEntity(Event ev) => new()
    {
        Id = ev.Id,
        Name = ev.Name,
        Type = ev.Type.ToString(),
        Location = ev.Location,
        ExpectedAttendance = ev.ExpectedAttendance,
        Notes = ev.Notes,
    };
}

/// <summary>
/// The booking request form (feature 3). Event details are supplied inline; the
/// service creates the Event and the Booking together so the client submits once.
/// </summary>
public sealed record CreateBookingRequest
{
    public required Guid CarId { get; init; }

    public required DateOnly StartDate { get; init; }

    public required DateOnly EndDate { get; init; }

    public string? ClientNotes { get; init; }

    /// <summary>Reuse an existing event to add a second car to the same activation.</summary>
    public Guid? ExistingEventId { get; init; }

    public string? EventName { get; init; }

    public EventType? EventType { get; init; }

    public string? EventLocation { get; init; }

    public int? ExpectedAttendance { get; init; }

    public string? EventNotes { get; init; }
}

public sealed record DecideBookingRequest
{
    /// <summary>Shown to the client in the notification, so it matters most on rejection.</summary>
    public string? Reason { get; init; }
}
