using FleetRental.Application.Abstractions;
using FleetRental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FleetRental.Application.Platform;

/// <summary>
/// Read side of the per-company feature toggles. A tenant with no override row
/// for a key is enabled — see <see cref="Domain.Entities.TenantFeatureToggle"/>
/// for why only overrides are stored.
/// </summary>
public class TenantFeatureGate(IFleetRentalDbContext db, ITenantContext tenantContext)
{
    /// <summary>
    /// Checks the current request's tenant. Read by <c>RequireFeatureAttribute</c>
    /// for controller-level gating and directly by anything that needs a plain
    /// boolean, like the push-notification fan-out.
    /// </summary>
    public async Task<bool> IsEnabledAsync(FeatureKey key, CancellationToken cancellationToken = default) =>
        tenantContext.HasTenant
            && await IsEnabledForAsync(tenantContext.TenantId!.Value, key, cancellationToken);

    /// <summary>Checks an explicit tenant, for callers (like notification delivery) not on the ambient request.</summary>
    public async Task<bool> IsEnabledForAsync(
        Guid tenantId,
        FeatureKey key,
        CancellationToken cancellationToken = default)
    {
        var toggle = await db.TenantFeatureToggles
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.FeatureKey == key, cancellationToken);

        // No row = default enabled.
        return toggle?.IsEnabled ?? true;
    }

    /// <summary>The current tenant's full feature map, for the client/admin app to know what to show.</summary>
    public async Task<IReadOnlyList<FeatureToggleDto>> ListForCurrentTenantAsync(
        CancellationToken cancellationToken = default)
    {
        if (!tenantContext.HasTenant)
        {
            return [.. Enum.GetValues<FeatureKey>().Select(key => new FeatureToggleDto { Key = key, IsEnabled = false })];
        }

        var overrides = await db.TenantFeatureToggles
            .AsNoTracking()
            .Where(t => t.TenantId == tenantContext.TenantId!.Value)
            .ToDictionaryAsync(t => t.FeatureKey, t => t.IsEnabled, cancellationToken);

        return
        [
            .. Enum.GetValues<FeatureKey>()
                .Select(key => new FeatureToggleDto { Key = key, IsEnabled = overrides.GetValueOrDefault(key, true) }),
        ];
    }
}
