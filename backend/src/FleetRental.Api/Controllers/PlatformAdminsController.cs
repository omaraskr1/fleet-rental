using FleetRental.Application.Common;
using FleetRental.Application.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetRental.Api.Controllers;

[ApiController]
[Route("api/platform/admins")]
[Authorize(Roles = PlatformRoles.PlatformAdmin)]
public class PlatformAdminsController(PlatformAdminService admins) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PlatformAdminDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PlatformAdminDto>>> GetAll(CancellationToken ct) =>
        Ok(await admins.ListAsync(ct));

    /// <summary>
    /// Provisions another platform admin. Every platform admin has equal standing —
    /// there is no further tier above this one.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<PlatformAdminDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PlatformAdminDto>> Create(CreatePlatformAdminRequest request, CancellationToken ct)
    {
        var admin = await admins.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), admin);
    }
}
