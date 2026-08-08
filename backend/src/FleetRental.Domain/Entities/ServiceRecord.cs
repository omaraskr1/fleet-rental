using FleetRental.Domain.Common;

namespace FleetRental.Domain.Entities;

/// <summary>
/// One completed piece of mechanical work on a car: routine service, a repair,
/// an inspection. The append-only log an owner needs to answer "what has been
/// done to this car, and when."
/// </summary>
/// <remarks>
/// Deliberately not a child collection on <see cref="Car"/> — like
/// <see cref="Booking"/>, it references the car by id rather than living in its
/// in-memory graph. A car's full service history can run to hundreds of rows
/// over its life and is queried independently (history screens, cost
/// aggregation); nothing about Car's own invariants needs it loaded eagerly.
/// </remarks>
public class ServiceRecord : TenantEntity
{
    private ServiceRecord() { } // EF Core

    private ServiceRecord(
        Guid carId,
        DateOnly performedAt,
        string description,
        int? odometerKm,
        decimal cost,
        string? performedBy)
    {
        CarId = carId;
        PerformedAt = performedAt;
        Description = description;
        OdometerKm = odometerKm;
        Cost = cost;
        PerformedBy = performedBy;
    }

    public Guid CarId { get; private set; }

    public Car Car { get; private set; } = null!;

    public DateOnly PerformedAt { get; private set; }

    /// <summary>What was done — "Oil and filter change", "Brake pad replacement".</summary>
    public string Description { get; private set; } = null!;

    /// <summary>
    /// Reading at the time of service. Nullable because not every entry (e.g. a
    /// windscreen chip repair) is naturally tied to distance driven, but this is
    /// what the next-service-due calculation anchors on when it is present.
    /// </summary>
    public int? OdometerKm { get; private set; }

    public decimal Cost { get; private set; }

    /// <summary>Workshop or mechanic name. Free text — a small fleet does not need a vendor table.</summary>
    public string? PerformedBy { get; private set; }

    public static ServiceRecord Log(
        Guid carId,
        DateOnly performedAt,
        string description,
        int? odometerKm,
        decimal cost,
        string? performedBy = null)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("Service description is required.");
        }

        if (odometerKm is < 0)
        {
            throw new DomainException("Odometer reading cannot be negative.");
        }

        if (cost < 0)
        {
            throw new DomainException("Service cost cannot be negative.");
        }

        // Logging a service done tomorrow is almost certainly a typo, and letting
        // it through would silently corrupt the next-service-due calculation,
        // which trusts the most recent PerformedAt to mean "already happened."
        if (performedAt > DateOnly.FromDateTime(DateTime.UtcNow.Date))
        {
            throw new DomainException("Service date cannot be in the future.");
        }

        return new ServiceRecord(carId, performedAt, description.Trim(), odometerKm, cost, performedBy?.Trim());
    }
}
