namespace FleetRental.Application.Abstractions;

/// <summary>
/// The tenant the current request belongs to. Scoped — one instance per request.
/// </summary>
/// <remarks>
/// Everything downstream reads isolation from here: the query filters, the
/// save-time tenant assignment, and any service that needs to scope work. Because
/// it is the single source, the rules about *where* the tenant may come from are
/// enforced in exactly one place (TenantResolutionMiddleware) rather than
/// scattered across controllers.
/// </remarks>
public interface ITenantContext
{
    /// <summary>Null before resolution, or on genuinely tenant-less routes.</summary>
    Guid? TenantId { get; }

    string? TenantCode { get; }

    bool HasTenant => TenantId is not null;

    /// <summary>
    /// Set once per request by the resolution middleware. Re-setting to a
    /// different tenant is refused — a request belongs to one business for its
    /// whole lifetime, and allowing a switch mid-flight would defeat the filters
    /// already applied to queries earlier in the same request.
    /// </summary>
    void SetTenant(Guid tenantId, string tenantCode);

    /// <summary>
    /// Suspends isolation for the duration of the returned scope. Only for
    /// genuine cross-tenant work: platform seeding, tenant lookup by code, and
    /// migrations. Every call site is a place isolation does not apply, so they
    /// should be few and obvious.
    /// </summary>
    IDisposable BypassIsolation();

    /// <summary>True while inside a <see cref="BypassIsolation"/> scope.</summary>
    bool IsolationBypassed { get; }
}
