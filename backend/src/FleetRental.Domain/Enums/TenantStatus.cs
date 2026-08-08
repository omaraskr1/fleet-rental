namespace FleetRental.Domain.Enums;

/// <summary>Subscription state of a rental business on the platform.</summary>
public enum TenantStatus
{
    Active = 0,

    /// <summary>Access cut off (non-payment, offboarding) without deleting data.</summary>
    Suspended = 1,
}
