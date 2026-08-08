using FleetRental.Domain.Common;
using FleetRental.Domain.Enums;

namespace FleetRental.Domain.Entities;

/// <summary>
/// An override of a <see cref="FeatureKey"/>'s default-on state for one company,
/// set by a platform admin.
/// </summary>
/// <remarks>
/// Only overrides are stored — a tenant with no row for a key is enabled, which is
/// what every company already using a feature needs to keep true with zero
/// migration data. See <see cref="Application.Platform.TenantFeatureGate"/> for the
/// read side and <see cref="Application.Platform.PlatformFeatureService"/> for how
/// a platform admin sets one.
/// </remarks>
public class TenantFeatureToggle : TenantEntity
{
    private TenantFeatureToggle() { } // EF Core

    private TenantFeatureToggle(FeatureKey featureKey, bool isEnabled)
    {
        FeatureKey = featureKey;
        IsEnabled = isEnabled;
    }

    public FeatureKey FeatureKey { get; private set; }

    public bool IsEnabled { get; private set; }

    public static TenantFeatureToggle Create(FeatureKey featureKey, bool isEnabled) =>
        new(featureKey, isEnabled);

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
        Touch();
    }
}
