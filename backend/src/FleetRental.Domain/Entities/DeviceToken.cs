using FleetRental.Domain.Common;
using FleetRental.Domain.Enums;

namespace FleetRental.Domain.Entities;

/// <summary>
/// A push notification target belonging to a <see cref="User"/>. Capacitor hands
/// the app an FCM/APNs token at runtime; this is where it is parked so the backend
/// can reach the device when a booking is decided.
/// </summary>
public class DeviceToken : TenantEntity
{
    private DeviceToken() { } // EF Core

    internal DeviceToken(Guid userId, string token, DevicePlatform platform, string deviceId)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new DomainException("Push token is required.");
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new DomainException("Device id is required.");
        }

        UserId = userId;
        Token = token;
        Platform = platform;
        DeviceId = deviceId;
    }

    public Guid UserId { get; private set; }

    public User User { get; private set; } = null!;

    /// <summary>Provider-issued token. Rotates, so it is updated rather than duplicated.</summary>
    public string Token { get; private set; } = null!;

    public DevicePlatform Platform { get; private set; }

    /// <summary>
    /// Stable per-install identifier. Unique per user so a token refresh updates
    /// the existing row instead of leaving a stale one behind.
    /// </summary>
    public string DeviceId { get; private set; } = null!;

    internal void UpdateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new DomainException("Push token is required.");
        }

        Token = token;
        Touch();
    }
}
