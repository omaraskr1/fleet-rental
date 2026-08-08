namespace FleetRental.Domain.Common;

/// <summary>
/// Base for every entity that belongs to a tenant.
/// </summary>
/// <remarks>
/// Inheriting rather than repeating the property in each entity means the
/// reassignment guard below is written once and cannot be forgotten. The
/// DbContext discovers tenant-owned types by this marker, so an entity that
/// derives from <see cref="Entity"/> instead of this one will fail the isolation
/// test in TenantIsolationTests rather than silently skipping its query filter.
/// </remarks>
public abstract class TenantEntity : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }

    /// <summary>
    /// Set once, when the entity is first persisted. Reassignment is refused
    /// outright: moving a row between tenants is never a legitimate operation, and
    /// if it happened by accident it would hand one business's data to another.
    /// </summary>
    public void AssignTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("Tenant id is required.");
        }

        if (TenantId != Guid.Empty && TenantId != tenantId)
        {
            throw new DomainException(
                $"{GetType().Name} {Id} already belongs to tenant {TenantId} and cannot be moved to {tenantId}.");
        }

        TenantId = tenantId;
    }
}
