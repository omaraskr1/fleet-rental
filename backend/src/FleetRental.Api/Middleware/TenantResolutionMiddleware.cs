using System.Security.Claims;
using FleetRental.Application.Abstractions;
using FleetRental.Domain.Entities;
using FleetRental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetRental.Api.Middleware;

/// <summary>
/// Establishes which tenant the request belongs to, before anything queries data.
/// </summary>
/// <remarks>
/// The precedence rule here is the single most security-sensitive decision in the
/// multi-tenant design:
///
///   1. If the caller is authenticated, the tenant comes from the JWT claim, and
///      the header is IGNORED entirely.
///   2. Only an anonymous caller may name a tenant via header, and then only to
///      browse the public catalogue or to reach a login screen.
///
/// Inverting that — letting a header win for an authenticated user — would let any
/// signed-in client read another rental company's data by changing one header.
/// The claim is signed; the header is not.
/// </remarks>
public class TenantResolutionMiddleware(RequestDelegate next)
{
    /// <summary>Claim carrying the tenant on every issued token.</summary>
    public const string TenantClaimType = "tenant_id";

    /// <summary>Header an anonymous caller uses to name the business they are browsing.</summary>
    public const string TenantHeader = "X-Tenant-Code";

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        FleetRentalDbContext db,
        ILogger<TenantResolutionMiddleware> logger)
    {
        var claim = context.User.FindFirst(TenantClaimType)?.Value;

        if (context.User.Identity?.IsAuthenticated == true && Guid.TryParse(claim, out var claimTenantId))
        {
            // Signed and issued by us — trusted, and it wins outright.
            var code = context.User.FindFirst("tenant_code")?.Value ?? string.Empty;
            tenantContext.SetTenant(claimTenantId, code);

            await next(context);
            return;
        }

        if (context.Request.Headers.TryGetValue(TenantHeader, out var headerValues))
        {
            var requested = Tenant.NormalizeCode(headerValues.ToString());

            // Tenants is not tenant-filtered, but this lookup runs before any
            // tenant is set, so the bypass makes the intent explicit rather than
            // relying on that being true.
            using var _ = tenantContext.BypassIsolation();

            var tenant = await db.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Code == requested);

            if (tenant is null)
            {
                logger.LogInformation("Unknown tenant code '{Code}' on {Path}.", requested, context.Request.Path);
            }
            else if (!tenant.IsActive)
            {
                logger.LogWarning("Suspended tenant '{Code}' attempted access.", requested);
            }
            else
            {
                tenantContext.SetTenant(tenant.Id, tenant.Code);
            }
        }

        // No tenant resolved is not an error here. The query filters fail closed,
        // so a tenant-scoped request simply finds nothing, and genuinely
        // tenant-less routes (health, tenant lookup) carry on unaffected.
        await next(context);
    }
}
