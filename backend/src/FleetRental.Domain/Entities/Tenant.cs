using FleetRental.Domain.Common;
using FleetRental.Domain.Enums;

namespace FleetRental.Domain.Entities;

/// <summary>
/// One rental business using the platform. The root of all isolation: every other
/// entity belongs to exactly one of these, and nothing crosses the boundary.
/// </summary>
/// <remarks>
/// Deliberately NOT <see cref="ITenantOwned"/> — it is the thing being owned by,
/// not owned. Its queries therefore bypass the global filter, which is why lookups
/// on it must always be by <see cref="Code"/> or id and never return a list to an
/// end user.
/// </remarks>
public class Tenant : Entity
{
    private Tenant() { } // EF Core

    private Tenant(string name, string code, string? contactEmail)
    {
        Name = name;
        Code = code;
        ContactEmail = contactEmail;
    }

    public string Name { get; private set; } = null!;

    /// <summary>
    /// Short public identifier a client types when first opening the app
    /// ("gulf-fleet"). Lowercase, unique across the platform, and stable — clients
    /// have it saved on their phones, so changing it locks them out.
    /// </summary>
    public string Code { get; private set; } = null!;

    public string? ContactEmail { get; private set; }

    public TenantStatus Status { get; private set; } = TenantStatus.Active;

    /// <summary>False when suspended — used to cut off access without deleting data.</summary>
    public bool IsActive => Status == TenantStatus.Active;

    public static Tenant Create(string name, string code, string? contactEmail = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Tenant name is required.");
        }

        var normalized = NormalizeCode(code);

        if (normalized.Length < 3)
        {
            throw new DomainException("Tenant code must be at least 3 characters.");
        }

        // Restricting the alphabet keeps the code safe to put in a URL, a QR code,
        // or a subdomain later without re-encoding it.
        if (!normalized.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
        {
            throw new DomainException("Tenant code may contain only letters, digits and hyphens.");
        }

        return new Tenant(name.Trim(), normalized, contactEmail?.Trim().ToLowerInvariant());
    }

    /// <summary>
    /// Canonical form for storage and lookup. Every comparison must route through
    /// here so "Gulf-Fleet" and "gulf-fleet" resolve to the same business.
    /// </summary>
    public static string NormalizeCode(string code) =>
        string.IsNullOrWhiteSpace(code)
            ? throw new DomainException("Tenant code is required.")
            : code.Trim().ToLowerInvariant();

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Tenant name is required.");
        }

        Name = name.Trim();
        Touch();
    }

    public void Suspend()
    {
        Status = TenantStatus.Suspended;
        Touch();
    }

    public void Reactivate()
    {
        Status = TenantStatus.Active;
        Touch();
    }
}
