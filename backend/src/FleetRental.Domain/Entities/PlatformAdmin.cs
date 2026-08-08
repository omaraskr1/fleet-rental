using FleetRental.Domain.Common;

namespace FleetRental.Domain.Entities;

/// <summary>
/// Operates across every tenant: creates companies, creates admins for them, and
/// monitors the whole fleet.
/// </summary>
/// <remarks>
/// Deliberately NOT a <see cref="TenantEntity"/> — a platform admin belongs to no
/// single company, the same way <see cref="Tenant"/> itself is not owned by one.
/// Kept as its own table rather than a <see cref="UserRole"/> value on
/// <see cref="User"/> so platform-level power stays structurally separate from
/// tenant-level power: a tenant admin can never become one by a role change, and a
/// platform admin never needs a (fake) tenant to belong to.
/// </remarks>
public class PlatformAdmin : Entity
{
    private PlatformAdmin() { } // EF Core

    private PlatformAdmin(string email, string passwordHash, string fullName)
    {
        Email = email;
        PasswordHash = passwordHash;
        FullName = fullName;
    }

    /// <summary>Login identity. Stored lowercase and uniquely indexed platform-wide.</summary>
    public string Email { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public string FullName { get; private set; } = null!;

    public bool IsActive { get; private set; } = true;

    public static PlatformAdmin Create(string email, string passwordHash, string fullName)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Password hash is required.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("Full name is required.");
        }

        return new PlatformAdmin(NormalizeEmail(email), passwordHash, fullName.Trim());
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    public void Reactivate()
    {
        IsActive = true;
        Touch();
    }
}
