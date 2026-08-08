using FleetRental.Application.Abstractions;
using FleetRental.Application.Common;
using FleetRental.Domain.Entities;
using FleetRental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FleetRental.Application.Platform;

/// <summary>
/// Platform-side management of per-company feature toggles. Bypasses isolation
/// for the same reason every other platform service does — the caller's token
/// carries no tenant to filter by, and touching an arbitrary company's rows is
/// the entire point.
/// </summary>
public class PlatformFeatureService(IFleetRentalDbContext db, ITenantContext tenantContext)
{
    /// <summary>All four keys, each merged with its override if one exists (default enabled).</summary>
    public async Task<IReadOnlyList<FeatureToggleDto>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        using var _ = tenantContext.BypassIsolation();

        if (!await db.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken))
        {
            throw new NotFoundException(nameof(Tenant), tenantId);
        }

        var overrides = await db.TenantFeatureToggles
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .ToDictionaryAsync(t => t.FeatureKey, t => t.IsEnabled, cancellationToken);

        return
        [
            .. Enum.GetValues<FeatureKey>()
                .Select(key => new FeatureToggleDto { Key = key, IsEnabled = overrides.GetValueOrDefault(key, true) }),
        ];
    }

    public async Task<FeatureToggleDto> SetAsync(
        Guid tenantId,
        FeatureKey key,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        using var _ = tenantContext.BypassIsolation();

        if (!await db.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken))
        {
            throw new NotFoundException(nameof(Tenant), tenantId);
        }

        var toggle = await db.TenantFeatureToggles
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.FeatureKey == key, cancellationToken);

        if (toggle is null)
        {
            toggle = TenantFeatureToggle.Create(key, isEnabled);
            toggle.AssignTenant(tenantId);
            db.TenantFeatureToggles.Add(toggle);
        }
        else
        {
            toggle.SetEnabled(isEnabled);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new FeatureToggleDto { Key = key, IsEnabled = isEnabled };
    }
}
