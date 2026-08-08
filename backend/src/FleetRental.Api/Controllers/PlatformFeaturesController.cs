using FleetRental.Application.Common;
using FleetRental.Application.Platform;
using FleetRental.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetRental.Api.Controllers;

[ApiController]
[Route("api/platform/companies/{tenantId:guid}/features")]
[Authorize(Roles = PlatformRoles.PlatformAdmin)]
public class PlatformFeaturesController(PlatformFeatureService features) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<FeatureToggleDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FeatureToggleDto>>> GetAll(Guid tenantId, CancellationToken ct) =>
        Ok(await features.ListAsync(tenantId, ct));

    [HttpPut("{key}")]
    [ProducesResponseType<FeatureToggleDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FeatureToggleDto>> Set(
        Guid tenantId,
        FeatureKey key,
        SetFeatureToggleRequest request,
        CancellationToken ct) =>
        Ok(await features.SetAsync(tenantId, key, request.IsEnabled, ct));
}
