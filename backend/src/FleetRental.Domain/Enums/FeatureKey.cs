namespace FleetRental.Domain.Enums;

/// <summary>
/// A feature a company's access to can be switched on or off by a platform admin.
/// Fixed and small on purpose — each value must correspond to something a
/// controller or service actually gates; see <see cref="Entities.TenantFeatureToggle"/>.
/// </summary>
public enum FeatureKey
{
    /// <summary>Revenue, utilization, profitability, and demand-forecasting dashboards.</summary>
    Analytics = 0,

    /// <summary>Vehicle issues, the service catalog, and per-car maintenance tracking.</summary>
    Maintenance = 1,

    /// <summary>Reserved: no code reads this yet. Location capture and the owner's map view.</summary>
    Gps = 2,

    /// <summary>Push delivery of booking decisions. Email is unaffected by this toggle.</summary>
    PushNotifications = 3,
}
