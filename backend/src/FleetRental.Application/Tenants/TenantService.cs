using FleetRental.Application.Abstractions;
using FleetRental.Application.Common;
using FleetRental.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetRental.Application.Tenants;

/// <summary>
/// Tenant lookup and provisioning.
/// </summary>
/// <remarks>
/// Reads here deliberately bypass isolation — this is how a client resolves which
/// business they are dealing with *before* any tenant is established. Everything
/// returned is therefore limited to what is safe to show an anonymous caller who
/// already knows the company code: the display name, nothing else.
/// </remarks>
public class TenantService(IFleetRentalDbContext db, ITenantContext tenantContext)
{
    /// <summary>
    /// Resolves a company code typed on first launch. Returns null rather than
    /// throwing so the app can show "we couldn't find that company" without the
    /// response distinguishing "wrong code" from "suspended account" — that
    /// difference is nobody's business but the owner's.
    /// </summary>
    public async Task<TenantSummaryDto?> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = Tenant.NormalizeCode(code);

        using var _ = tenantContext.BypassIsolation();

        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Code == normalized, cancellationToken);

        return tenant is { IsActive: true }
            ? new TenantSummaryDto { Code = tenant.Code, Name = tenant.Name }
            : null;
    }

    /// <summary>
    /// Onboards a new rental business. Platform-level, not tenant-scoped — the
    /// API restricts this to a platform key rather than any tenant's admin.
    /// </summary>
    public async Task<TenantSummaryDto> CreateAsync(
        CreateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        using var _ = tenantContext.BypassIsolation();

        var code = Tenant.NormalizeCode(request.Code);

        if (await db.Tenants.AnyAsync(t => t.Code == code, cancellationToken))
        {
            throw ValidationException.Single(nameof(request.Code), "That company code is already taken.");
        }

        var tenant = Tenant.Create(request.Name, request.Code, request.ContactEmail);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);

        return new TenantSummaryDto { Code = tenant.Code, Name = tenant.Name };
    }
}

/// <summary>What an anonymous caller may learn about a tenant from its code.</summary>
public sealed record TenantSummaryDto
{
    public required string Code { get; init; }

    public required string Name { get; init; }
}

public sealed record CreateTenantRequest
{
    public required string Name { get; init; }

    public required string Code { get; init; }

    public string? ContactEmail { get; init; }
}
