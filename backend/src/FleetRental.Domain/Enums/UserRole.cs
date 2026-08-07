namespace FleetRental.Domain.Enums;

/// <summary>
/// Access level. Phase 3 (multi-admin) adds roles here rather than changing the
/// shape of <see cref="Entities.User"/>.
/// </summary>
public enum UserRole
{
    /// <summary>Books cars. Sees only their own requests.</summary>
    Client = 0,

    /// <summary>Fleet owner. Approves/rejects requests and manages cars.</summary>
    Admin = 1,
}
