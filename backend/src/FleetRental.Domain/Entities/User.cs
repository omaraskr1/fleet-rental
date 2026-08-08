using FleetRental.Domain.Common;
using FleetRental.Domain.Enums;

namespace FleetRental.Domain.Entities;

/// <summary>
/// A client or fleet owner. Both roles live in one table with a
/// <see cref="UserRole"/> discriminator: the login screens are separate in the UI,
/// but a single identity store keeps token issuance and lookups uniform.
/// </summary>
public class User : TenantEntity
{
    private readonly List<Booking> _bookings = [];
    private readonly List<DeviceToken> _deviceTokens = [];

    private User() { } // EF Core

    private User(string email, string passwordHash, string fullName, string? phoneNumber, UserRole role)
    {
        Email = email;
        PasswordHash = passwordHash;
        FullName = fullName;
        PhoneNumber = phoneNumber;
        Role = role;
    }

    /// <summary>Login identity. Stored lowercase and uniquely indexed.</summary>
    public string Email { get; private set; } = null!;

    /// <summary>
    /// Opaque hash produced by the Infrastructure layer. The domain never learns
    /// which algorithm was used, so it can be upgraded without touching this class.
    /// </summary>
    public string PasswordHash { get; private set; } = null!;

    public string FullName { get; private set; } = null!;

    /// <summary>Optional in Phase 1; becomes required when WhatsApp lands in Phase 2.</summary>
    public string? PhoneNumber { get; private set; }

    public UserRole Role { get; private set; }

    /// <summary>Soft-disable so a user's booking history survives deactivation.</summary>
    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<Booking> Bookings => _bookings.AsReadOnly();

    /// <summary>Registered push targets. One row per device the user signs in on.</summary>
    public IReadOnlyCollection<DeviceToken> DeviceTokens => _deviceTokens.AsReadOnly();

    public static User RegisterClient(string email, string passwordHash, string fullName, string? phoneNumber = null) =>
        Create(email, passwordHash, fullName, phoneNumber, UserRole.Client);

    public static User RegisterAdmin(string email, string passwordHash, string fullName, string? phoneNumber = null) =>
        Create(email, passwordHash, fullName, phoneNumber, UserRole.Admin);

    private static User Create(string email, string passwordHash, string fullName, string? phoneNumber, UserRole role)
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

        return new User(NormalizeEmail(email), passwordHash, fullName.Trim(), phoneNumber?.Trim(), role);
    }

    /// <summary>
    /// Canonical form used for both storage and lookup. Callers must route every
    /// comparison through this so "A@x.com" and "a@x.com" resolve to one account.
    /// </summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public void UpdateProfile(string fullName, string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("Full name is required.");
        }

        FullName = fullName.Trim();
        PhoneNumber = phoneNumber?.Trim();
        Touch();
    }

    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            throw new DomainException("Password hash is required.");
        }

        PasswordHash = newPasswordHash;
        Touch();
    }

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

    /// <summary>
    /// Registers a push token, replacing any existing entry for the same device so
    /// reinstalls do not accumulate dead tokens.
    /// </summary>
    public void RegisterDeviceToken(string token, DevicePlatform platform, string deviceId)
    {
        var existing = _deviceTokens.FirstOrDefault(t => t.DeviceId == deviceId);
        if (existing is not null)
        {
            existing.UpdateToken(token);
            return;
        }

        _deviceTokens.Add(new DeviceToken(Id, token, platform, deviceId));
        Touch();
    }

    public void RemoveDeviceToken(string deviceId)
    {
        var existing = _deviceTokens.FirstOrDefault(t => t.DeviceId == deviceId);
        if (existing is not null)
        {
            _deviceTokens.Remove(existing);
            Touch();
        }
    }
}
