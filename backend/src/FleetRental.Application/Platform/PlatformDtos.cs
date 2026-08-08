using FleetRental.Domain.Entities;

namespace FleetRental.Application.Platform;

public sealed record PlatformLoginRequest
{
    public required string Email { get; init; }

    public required string Password { get; init; }
}

public sealed record PlatformAuthResponse
{
    public required string AccessToken { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public required PlatformAdminDto Admin { get; init; }
}

public sealed record PlatformAdminDto
{
    public required Guid Id { get; init; }

    public required string Email { get; init; }

    public required string FullName { get; init; }

    public required bool IsActive { get; init; }

    public static PlatformAdminDto FromEntity(PlatformAdmin admin) => new()
    {
        Id = admin.Id,
        Email = admin.Email,
        FullName = admin.FullName,
        IsActive = admin.IsActive,
    };
}

public sealed record CreatePlatformAdminRequest
{
    public required string Email { get; init; }

    public required string Password { get; init; }

    public required string FullName { get; init; }
}

/// <summary>
/// Richer than <see cref="Tenants.TenantSummaryDto"/> (which is deliberately
/// minimal — it's what an anonymous caller may learn). Platform admins are
/// authenticated and need the id (to target suspend/reactivate/admins/cars
/// actions) and the operational status.
/// </summary>
public sealed record CompanyDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Code { get; init; }

    public string? ContactEmail { get; init; }

    public required string Status { get; init; }

    public static CompanyDto FromEntity(Tenant tenant) => new()
    {
        Id = tenant.Id,
        Name = tenant.Name,
        Code = tenant.Code,
        ContactEmail = tenant.ContactEmail,
        Status = tenant.Status.ToString(),
    };
}

public sealed record CreateCompanyAdminRequest
{
    public required string Email { get; init; }

    public required string Password { get; init; }

    public required string FullName { get; init; }
}

public sealed record CompanyAdminDto
{
    public required Guid Id { get; init; }

    public required string Email { get; init; }

    public required string FullName { get; init; }

    public required bool IsActive { get; init; }

    public static CompanyAdminDto FromEntity(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FullName = user.FullName,
        IsActive = user.IsActive,
    };
}
