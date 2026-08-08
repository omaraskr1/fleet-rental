using FleetRental.Application.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetRental.Api.Controllers;

/// <summary>The current tenant's feature map, so the client/admin app knows what to show.</summary>
[ApiController]
[Route("api/features")]
[Authorize]
public class FeaturesController(TenantFeatureGate features) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<FeatureToggleDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FeatureToggleDto>>> GetAll(CancellationToken ct) =>
        Ok(await features.ListForCurrentTenantAsync(ct));
}
