namespace FleetRental.Domain.Common;

/// <summary>
/// Marks an entity as belonging to exactly one tenant.
/// </summary>
/// <remarks>
/// Every persisted entity except <see cref="Entities.Tenant"/> itself implements
/// this. Making it a uniform rule rather than a per-entity judgement call is
/// deliberate: the DbContext applies a global query filter to everything carrying
/// this marker, so "which entities need isolating?" never has to be answered again
/// — and a new entity cannot quietly opt out of isolation by being forgotten.
/// </remarks>
public interface ITenantOwned
{
    Guid TenantId { get; }

    /// <summary>
    /// Assigns the owning tenant. Called by the persistence layer when the entity
    /// is first saved, so domain factories stay free of ambient context. Never
    /// call this to move an entity between tenants.
    /// </summary>
    void AssignTenant(Guid tenantId);
}
