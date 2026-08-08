using FleetRental.Api.Extensions;
using FleetRental.Application.Common;
using FleetRental.Application.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetRental.Api.Controllers;

[ApiController]
[Route("api/platform/auth")]
public class PlatformAuthController(PlatformAdminService admins) : ControllerBase
{
    /// <summary>Login for the platform panel — entirely separate from tenant auth.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<PlatformAuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PlatformAuthResponse>> Login(PlatformLoginRequest request, CancellationToken ct) =>
        Ok(await admins.LoginAsync(request, ct));

    /// <summary>The signed-in platform admin, for restoring session state on app launch.</summary>
    [HttpGet("me")]
    [Authorize(Roles = PlatformRoles.PlatformAdmin)]
    [ProducesResponseType<PlatformAdminDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PlatformAdminDto>> Me(CancellationToken ct) =>
        Ok(await admins.GetByIdAsync(User.GetUserId(), ct));
}
