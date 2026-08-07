namespace FleetRental.Domain.Enums;

/// <summary>
/// How urgently a reported vehicle problem needs attention. Drives sort order
/// and highlighting on the owner's issue list — a Critical issue on a car that
/// is about to go out on a booking is the thing an owner most needs to see first.
/// </summary>
public enum IssueSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,

    /// <summary>Car should not go out until resolved.</summary>
    Critical = 3,
}
