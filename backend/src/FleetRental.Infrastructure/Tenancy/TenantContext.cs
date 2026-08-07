using FleetRental.Application.Abstractions;
using FleetRental.Application.Common;

namespace FleetRental.Infrastructure.Tenancy;

/// <inheritdoc />
public class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public string? TenantCode { get; private set; }

    public bool IsolationBypassed { get; private set; }

    public void SetTenant(Guid tenantId, string tenantCode)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id cannot be empty.", nameof(tenantId));
        }

        // Switching tenants mid-request would leave queries made before the switch
        // filtered to the old tenant and queries after it to the new one, which is
        // exactly the kind of partial isolation that leaks data.
        if (TenantId is not null && TenantId != tenantId)
        {
            throw new ForbiddenException("The tenant for this request has already been resolved.");
        }

        TenantId = tenantId;
        TenantCode = tenantCode;
    }

    public IDisposable BypassIsolation()
    {
        IsolationBypassed = true;
        return new BypassScope(this);
    }

    private sealed class BypassScope(TenantContext context) : IDisposable
    {
        public void Dispose() => context.IsolationBypassed = false;
    }
}
