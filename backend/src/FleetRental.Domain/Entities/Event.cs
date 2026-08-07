using FleetRental.Domain.Common;
using FleetRental.Domain.Enums;

namespace FleetRental.Domain.Entities;

/// <summary>
/// The occasion a client is renting for.
/// </summary>
/// <remarks>
/// Modelled as its own entity rather than as fields on <see cref="Booking"/>
/// because a single marketing activation routinely needs several vehicles: one
/// event, several bookings. Flattening it into Booking would duplicate the event
/// details per car and make "show me everything for this activation" a text-matching
/// exercise. The Phase 1 UI still creates the event inline with the booking, so
/// clients never see the extra concept.
/// </remarks>
public class Event : Entity
{
    private readonly List<Booking> _bookings = [];

    private Event() { } // EF Core

    private Event(
        Guid organizerId,
        string name,
        EventType type,
        string location,
        int? expectedAttendance,
        string? notes)
    {
        OrganizerId = organizerId;
        Name = name;
        Type = type;
        Location = location;
        ExpectedAttendance = expectedAttendance;
        Notes = notes;
    }

    /// <summary>The client who created the event.</summary>
    public Guid OrganizerId { get; private set; }

    public User Organizer { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public EventType Type { get; private set; }

    /// <summary>Where the cars are needed. Free text in Phase 1.</summary>
    public string Location { get; private set; } = null!;

    public int? ExpectedAttendance { get; private set; }

    /// <summary>Client-supplied context for the fleet owner (branding, drivers, access).</summary>
    public string? Notes { get; private set; }

    /// <summary>Every car booked for this event.</summary>
    public IReadOnlyCollection<Booking> Bookings => _bookings.AsReadOnly();

    public static Event Create(
        Guid organizerId,
        string name,
        EventType type,
        string location,
        int? expectedAttendance = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Event name is required.");
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            throw new DomainException("Event location is required.");
        }

        if (expectedAttendance is < 0)
        {
            throw new DomainException("Expected attendance cannot be negative.");
        }

        return new Event(
            organizerId,
            name.Trim(),
            type,
            location.Trim(),
            expectedAttendance,
            notes?.Trim());
    }

    public void UpdateDetails(
        string name,
        EventType type,
        string location,
        int? expectedAttendance,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Event name is required.");
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            throw new DomainException("Event location is required.");
        }

        if (expectedAttendance is < 0)
        {
            throw new DomainException("Expected attendance cannot be negative.");
        }

        Name = name.Trim();
        Type = type;
        Location = location.Trim();
        ExpectedAttendance = expectedAttendance;
        Notes = notes?.Trim();
        Touch();
    }
}
