namespace FleetRental.Domain.Enums;

/// <summary>
/// How a car's <c>Rate</c> should be read. Persisted as string, same reasoning
/// as the other fleet enums — a value added later must not renumber existing rows.
/// </summary>
public enum PricingModel
{
    /// <summary>Rate applies once per calendar day of the booking.</summary>
    PerDay = 0,

    /// <summary>Rate applies once per booking, regardless of how many days it spans.</summary>
    PerEvent = 1,
}
