using FleetRental.Domain.Common;

namespace FleetRental.Domain.Entities;

/// <summary>
/// One GPS reading reported by the physical device installed in a car. The
/// append-only trail an owner needs to answer "where is this car right now" —
/// and, over time, "where has it been."
/// </summary>
/// <remarks>
/// Deliberately not a child collection on <see cref="Car"/> — same reasoning as
/// <see cref="ServiceRecord"/>: a device pinging every few seconds can produce
/// thousands of rows over a car's life, queried independently (the map's
/// "latest per car" query, and later a trail/history view), so nothing about
/// Car's own invariants needs it loaded eagerly.
/// </remarks>
public class CarLocation : TenantEntity
{
    private CarLocation() { } // EF Core

    private CarLocation(Guid carId, double latitude, double longitude, DateTimeOffset recordedAt)
    {
        CarId = carId;
        Latitude = latitude;
        Longitude = longitude;
        RecordedAt = recordedAt;
    }

    public Guid CarId { get; private set; }

    public Car Car { get; private set; } = null!;

    public double Latitude { get; private set; }

    public double Longitude { get; private set; }

    /// <summary>When the device took the reading, not when the server received it.</summary>
    public DateTimeOffset RecordedAt { get; private set; }

    public static CarLocation Log(Guid carId, double latitude, double longitude, DateTimeOffset recordedAt)
    {
        if (latitude is < -90 or > 90)
        {
            throw new DomainException("Latitude must be between -90 and 90.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new DomainException("Longitude must be between -180 and 180.");
        }

        // A reading from the future is almost certainly a clock-sync problem on
        // the device, not a real position — and would sort ahead of genuine
        // readings in "most recent" queries, permanently hiding them.
        if (recordedAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            throw new DomainException("Recorded time cannot be in the future.");
        }

        return new CarLocation(carId, latitude, longitude, recordedAt);
    }
}
