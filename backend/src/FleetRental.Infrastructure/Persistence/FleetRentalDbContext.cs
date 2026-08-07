using System.Reflection;
using FleetRental.Application.Abstractions;
using FleetRental.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FleetRental.Infrastructure.Persistence;

public class FleetRentalDbContext(DbContextOptions<FleetRentalDbContext> options)
    : DbContext(options), IFleetRentalDbContext
{
    public DbSet<Car> Cars => Set<Car>();

    public DbSet<CarPhoto> CarPhotos => Set<CarPhoto>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<BookedDay> BookedDays => Set<BookedDay>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<User> Users => Set<User>();

    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Every Id is assigned by Entity's constructor, but EF defaults Guid keys to
        // store-generated. That mismatch is silently destructive: when a child is
        // added to an already-tracked parent (BookedDay rows during booking
        // approval), EF sees a non-default key, concludes the row must already
        // exist, and emits an UPDATE that matches nothing — surfacing as
        // DbUpdateConcurrencyException instead of inserting.
        //
        // Applied by convention rather than per-configuration so a new entity
        // cannot be added without it.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(Domain.Common.Entity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(Domain.Common.Entity.Id))
                .ValueGeneratedNever();
        }

        base.OnModelCreating(modelBuilder);
    }

    public async Task<T> InTransactionAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        // An ambient transaction means we are already inside one (a caller composed
        // two operations); joining it rather than nesting keeps the outer scope in
        // charge of committing.
        if (Database.CurrentTransaction is not null)
        {
            return await action(cancellationToken);
        }

        var strategy = Database.CreateExecutionStrategy();

        // The retrying execution strategy owns the whole transaction, so a transient
        // failure replays the entire unit rather than half of it.
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var result = await action(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    /// <summary>
    /// SQL Server reports a unique-index violation as error 2601 and a unique
    /// constraint violation as 2627. Both mean the same thing to us: a competing
    /// write claimed the row first.
    /// </summary>
    public bool IsUniqueConstraintViolation(Exception exception)
    {
        var sqlException = exception as SqlException
            ?? exception.InnerException as SqlException;

        return sqlException?.Number is 2601 or 2627;
    }
}
