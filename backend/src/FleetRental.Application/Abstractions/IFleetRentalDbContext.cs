using FleetRental.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetRental.Application.Abstractions;

/// <summary>
/// The persistence surface the Application layer is allowed to see. Infrastructure
/// supplies the real DbContext; keeping the interface here is what stops
/// Application from referencing Infrastructure and inverting the dependency arrow.
/// </summary>
public interface IFleetRentalDbContext
{
    /// <summary>
    /// The tenant registry. Not tenant-filtered — it is the root of isolation, so
    /// reads here must always be by code or id, never an unbounded list.
    /// </summary>
    DbSet<Tenant> Tenants { get; }

    DbSet<Car> Cars { get; }

    DbSet<CarPhoto> CarPhotos { get; }

    DbSet<Booking> Bookings { get; }

    DbSet<BookedDay> BookedDays { get; }

    DbSet<Event> Events { get; }

    DbSet<User> Users { get; }

    /// <summary>
    /// Operators who work across every tenant. Not tenant-filtered, like
    /// <see cref="Tenants"/> — reads here must always be by id or email.
    /// </summary>
    DbSet<PlatformAdmin> PlatformAdmins { get; }

    DbSet<DeviceToken> DeviceTokens { get; }

    DbSet<ServiceRecord> ServiceRecords { get; }

    DbSet<ServiceType> ServiceTypes { get; }

    DbSet<VehicleIssue> VehicleIssues { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="action"/> inside a database transaction.
    /// Booking approval needs this: the availability re-check and the day claims
    /// must commit or roll back together, or the guarantee is worthless.
    /// </summary>
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the failure was a unique-constraint violation. Recognising it means
    /// reading provider-specific error numbers, which is Infrastructure's business —
    /// the Application layer only needs to know "someone got there first" so it can
    /// return 409 instead of 500.
    /// </summary>
    bool IsUniqueConstraintViolation(Exception exception);
}
