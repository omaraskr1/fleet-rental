namespace FleetRental.Application.Common;

/// <summary>
/// Role string carried on a platform admin's JWT. Not a <c>UserRole</c> value —
/// platform admins are not <c>User</c>s — so this lives outside that enum but
/// still drives <c>[Authorize(Roles = PlatformRoles.PlatformAdmin)]</c> the same
/// way <c>UserRole</c> drives tenant-scoped authorization.
/// </summary>
public static class PlatformRoles
{
    public const string PlatformAdmin = "PlatformAdmin";
}
