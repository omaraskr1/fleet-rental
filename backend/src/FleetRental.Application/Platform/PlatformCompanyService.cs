using FleetRental.Application.Abstractions;
using FleetRental.Application.Common;
using FleetRental.Application.Tenants;
using FleetRental.Domain.Entities;
using FleetRental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FleetRental.Application.Platform;

/// <summary>
/// Company (tenant) management for the platform panel: list every company, create
/// new ones, suspend/reactivate, and provision each company's own admins.
/// </summary>
/// <remarks>
/// Every method here bypasses tenant isolation deliberately — a platform admin's
/// JWT carries no tenant, so without the bypass every query below would simply
/// match nothing (the filter's fail-closed behavior). That's correct for a normal
/// request and exactly wrong for this one, which needs to see and touch every
/// tenant on purpose.
/// </remarks>
public class PlatformCompanyService(
    IFleetRentalDbContext db,
    IPasswordHasher passwordHasher,
    ITenantContext tenantContext)
{
    private const int MinimumPasswordLength = 8;

    public async Task<IReadOnlyList<CompanyDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var _ = tenantContext.BypassIsolation();

        var tenants = await db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return [.. tenants.Select(CompanyDto.FromEntity)];
    }

    public async Task<CompanyDto> CreateAsync(
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

        return CompanyDto.FromEntity(tenant);
    }

    public async Task<CompanyDto> SuspendAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var _ = tenantContext.BypassIsolation();

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        tenant.Suspend();
        await db.SaveChangesAsync(cancellationToken);

        return CompanyDto.FromEntity(tenant);
    }

    public async Task<CompanyDto> ReactivateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var _ = tenantContext.BypassIsolation();

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        tenant.Reactivate();
        await db.SaveChangesAsync(cancellationToken);

        return CompanyDto.FromEntity(tenant);
    }

    public async Task<IReadOnlyList<CompanyAdminDto>> ListAdminsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        using var _ = tenantContext.BypassIsolation();

        if (!await db.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken))
        {
            throw new NotFoundException(nameof(Tenant), tenantId);
        }

        var admins = await db.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.Role == UserRole.Admin)
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);

        return [.. admins.Select(CompanyAdminDto.FromEntity)];
    }

    /// <summary>
    /// Creates an admin inside a specific company from platform context — the
    /// counterpart to <c>DbSeeder.SeedAdminAsync</c>, but reachable through the API
    /// once at least one platform admin exists to call it.
    /// </summary>
    public async Task<CompanyAdminDto> CreateAdminAsync(
        Guid tenantId,
        CreateCompanyAdminRequest request,
        CancellationToken cancellationToken = default)
    {
        using var _ = tenantContext.BypassIsolation();

        if (!await db.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken))
        {
            throw new NotFoundException(nameof(Tenant), tenantId);
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < MinimumPasswordLength)
        {
            throw ValidationException.Single(
                nameof(request.Password),
                $"Password must be at least {MinimumPasswordLength} characters.");
        }

        var email = User.NormalizeEmail(request.Email);

        if (await db.Users.AnyAsync(u => u.TenantId == tenantId && u.Email == email, cancellationToken))
        {
            throw ValidationException.Single(nameof(request.Email), "An account with this email already exists.");
        }

        var admin = User.RegisterAdmin(email, passwordHasher.Hash(request.Password), request.FullName);
        admin.AssignTenant(tenantId);

        db.Users.Add(admin);
        await db.SaveChangesAsync(cancellationToken);

        return CompanyAdminDto.FromEntity(admin);
    }
}
