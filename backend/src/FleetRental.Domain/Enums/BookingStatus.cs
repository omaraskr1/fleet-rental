namespace FleetRental.Domain.Enums;

/// <summary>
/// Booking request lifecycle. Only <see cref="Approved"/> blocks dates for other
/// clients — pending requests may overlap freely until an admin decides.
/// </summary>
public enum BookingStatus
{
    /// <summary>Submitted by a client, awaiting admin decision.</summary>
    Pending = 0,

    /// <summary>Admin approved. Holds the date range exclusively.</summary>
    Approved = 1,

    /// <summary>Admin rejected. Releases nothing since it never held dates.</summary>
    Rejected = 2,

    /// <summary>Withdrawn by the client before a decision was made.</summary>
    Cancelled = 3,
}
