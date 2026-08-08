using FleetRental.Application.Gps;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetRental.Api.Controllers;

/// <summary>
/// The device-facing side of GPS tracking. Anonymous by necessity — the
/// physical unit installed in a car has no user account, only the key it was
/// configured with.
/// </summary>
[ApiController]
[Route("api/gps")]
public class GpsController(GpsService gps) : ControllerBase
{
    [HttpPost("locations")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReportLocation(ReportLocationRequest request, CancellationToken ct)
    {
        await gps.ReportLocationAsync(request, ct);
        return NoContent();
    }
}
