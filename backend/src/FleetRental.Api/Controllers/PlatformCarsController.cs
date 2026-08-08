using FleetRental.Application.Cars;
using FleetRental.Application.Common;
using FleetRental.Application.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetRental.Api.Controllers;

[ApiController]
[Route("api/platform/cars")]
[Authorize(Roles = PlatformRoles.PlatformAdmin)]
public class PlatformCarsController(PlatformCarService cars) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PlatformCarDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PlatformCarDto>>> GetAll(CancellationToken ct) =>
        Ok(await cars.ListAsync(ct));

    [HttpPost]
    [ProducesResponseType<PlatformCarDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PlatformCarDto>> Create(CreatePlatformCarRequest request, CancellationToken ct)
    {
        var car = await cars.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), car);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<PlatformCarDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PlatformCarDto>> Update(Guid id, UpdateCarRequest request, CancellationToken ct) =>
        Ok(await cars.UpdateAsync(id, request, ct));

    /// <summary>Retires the car. Never a hard delete — booking history references it.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Retire(Guid id, CancellationToken ct)
    {
        await cars.RetireAsync(id, ct);
        return NoContent();
    }
}
