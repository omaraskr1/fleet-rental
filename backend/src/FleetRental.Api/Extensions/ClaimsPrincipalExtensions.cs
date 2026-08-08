using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FleetRental.Application.Common;
using FleetRental.Domain.Enums;

namespace FleetRental.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The authenticated user's id, taken from the token rather than from anything
    /// the caller sent. Endpoints that scope data to "me" must use this — reading
    /// a user id from the route or body would let one client read another's bookings.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var id)
            ? id
            : throw new UnauthorizedAccessException("Token does not carry a valid user id.");
    }

    public static bool IsAdmin(this ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(UserRole.Admin));

    public static bool IsPlatformAdmin(this ClaimsPrincipal principal) =>
        principal.IsInRole(PlatformRoles.PlatformAdmin);
}
