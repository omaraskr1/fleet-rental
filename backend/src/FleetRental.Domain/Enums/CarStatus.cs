namespace FleetRental.Domain.Enums;

/// <summary>
/// Fleet-owner controlled lifecycle of a vehicle. Distinct from per-date
/// availability, which is derived from bookings rather than stored.
/// </summary>
public enum CarStatus
{
    /// <summary>Listed and accepting booking requests.</summary>
    Active = 0,

    /// <summary>Temporarily out of service (servicing, repairs). Hidden from browse.</summary>
    Maintenance = 1,

    /// <summary>Removed from the fleet. Retained for booking history.</summary>
    Retired = 2,
}
