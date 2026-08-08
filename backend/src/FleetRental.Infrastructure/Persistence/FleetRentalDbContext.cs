using System.Linq.Expressions;
using System.Reflection;
using FleetRental.Application.Abstractions;
using FleetRental.Domain.Common;
using FleetRental.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FleetRental.Infrastructure.Persistence;

public class FleetRentalDbContext(
    DbContextOptions<FleetRentalDbContext> options,
    ITenantContext tenantContext)
    : DbContext(options), IFleetRentalDbContext
{
    /// <summary>
    /// Read by the global query filters. EF turns this into a query parameter, so
    /// the compiled model is shared across requests while the value stays
    /// per-request.
    /// </summary>
    /// <remarks>
    /// Non-nullable on purpose. An unresolved tenant collapses to Guid.Empty,
    /// which no row can carry — so the filter matches nothing and fails closed.
    /// Exposing this as Guid? instead forced a null-coalesce into the filter
    /// expression, and the resulting Guid?-to-Guid conversion threw
    /// "Nullable object must have a value" on every tenant-less request.
    /// </remarks>
    private Guid CurrentTenantId => tenantContext.TenantId ?? Guid.Empty;

    /// <summary>
    /// When true the filters let everything through. Only genuine platform-level
    /// work sets this — see <see cref="ITenantContext.BypassIsolation"/>.
    /// </summary>
    private bool IsolationBypassed => tenantContext.IsolationBypassed;

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Car> Cars => Set<Car>();

    public DbSet<CarPhoto> CarPhotos => Set<CarPhoto>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<BookedDay> BookedDays => Set<BookedDay>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<User> Users => Set<User>();

    public DbSet<ServiceRecord> ServiceRecords => Set<ServiceRecord>();

    public DbSet<VehicleIssue> VehicleIssues => Set<VehicleIssue>();

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

        ApplyTenantIsolation(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Applies a tenant filter to every <see cref="ITenantOwned"/> entity.
    /// </summary>
    /// <remarks>
    /// This is the mechanism that makes shared-database tenancy safe. Rather than
    /// trusting every query in the codebase to remember a tenant predicate, the
    /// predicate is attached to the model itself — so a query that forgets it is
    /// still filtered.
    ///
    /// The filter fails CLOSED: when no tenant is resolved it matches nothing
    /// rather than everything. A bug that leaves the tenant unset therefore
    /// produces an obvious empty result, not a silent cross-tenant data leak.
    /// </remarks>
    private void ApplyTenantIsolation(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            // Built by hand rather than with a generic helper because the CLR type
            // is only known at runtime here.
            var parameter = Expression.Parameter(entityType.ClrType, "e");

            var tenantIdProperty = Expression.Property(parameter, nameof(ITenantOwned.TenantId));

            var currentTenantId = Expression.Property(
                Expression.Constant(this), nameof(CurrentTenantId));

            var bypassed = Expression.Property(
                Expression.Constant(this), nameof(IsolationBypassed));

            // e => IsolationBypassed || e.TenantId == CurrentTenantId
            // Both sides are non-nullable Guid, so no conversion is introduced.
            var comparison = Expression.Equal(tenantIdProperty, currentTenantId);

            var body = Expression.OrElse(bypassed, comparison);

            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(Expression.Lambda(body, parameter));
        }
    }

    /// <summary>
    /// Stamps the current tenant onto anything newly added, so no caller has to
    /// remember — the counterpart to the read-side query filter.
    /// </summary>
    private void AssignTenantToNewEntities()
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
            {
                entry.Entity.AssignTenant(tenantId);
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AssignTenantToNewEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        AssignTenantToNewEntities();
        return base.SaveChanges();
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
