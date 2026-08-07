using FleetRental.Api.Extensions;
using FleetRental.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetRental.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    /// <summary>Client signup (feature 5). Always creates a Client — never an Admin.</summary>
    [HttpPost("signup")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> SignUp(SignUpRequest request, CancellationToken ct) =>
        Ok(await auth.SignUpAsync(request, ct));

    /// <summary>Client login.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await auth.LoginAsync(request, ct));

    /// <summary>
    /// Admin login for the web panel. Separate endpoint so a client account gets a
    /// clear 403 instead of a token the panel would silently refuse to use.
    /// </summary>
    [HttpPost("admin/login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> AdminLogin(LoginRequest request, CancellationToken ct) =>
        Ok(await auth.AdminLoginAsync(request, ct));

    /// <summary>The signed-in user, for restoring session state on app launch.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct) =>
        Ok(await auth.GetByIdAsync(User.GetUserId(), ct));

    /// <summary>Registers this device for push (feature 6).</summary>
    [HttpPost("devices")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RegisterDevice(RegisterDeviceRequest request, CancellationToken ct)
    {
        await auth.RegisterDeviceAsync(User.GetUserId(), request, ct);
        return NoContent();
    }

    /// <summary>Unregisters on sign-out so the device stops receiving pushes.</summary>
    [HttpDelete("devices/{deviceId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UnregisterDevice(string deviceId, CancellationToken ct)
    {
        await auth.UnregisterDeviceAsync(User.GetUserId(), deviceId, ct);
        return NoContent();
    }
}
