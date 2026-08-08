using FleetRental.Application.Tenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetRental.Api.Controllers;

[ApiController]
[Route("api/tenants")]
public class TenantsController(TenantService tenants, IConfiguration configuration) : ControllerBase
{
    /// <summary>
    /// Resolves the company code a client types on first launch.
    /// </summary>
    /// <remarks>
    /// Anonymous by necessity — it runs before any account exists. It returns only
    /// the display name so the app can show "Gulf Fleet — is this right?", and a
    /// flat 404 for anything unknown or suspended so the endpoint cannot be used
    /// to enumerate which businesses are on the platform.
    /// </remarks>
    [HttpGet("{code}")]
    [AllowAnonymous]
    [ProducesResponseType<TenantSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantSummaryDto>> GetByCode(string code, CancellationToken ct)
    {
        var tenant = await tenants.FindByCodeAsync(code, ct);
        return tenant is null ? NotFound() : Ok(tenant);
    }

    /// <summary>
    /// Onboards a rental business. Platform-level, so it is gated on a
    /// provisioning key rather than on any tenant's admin role — a tenant admin
    /// must never be able to create other tenants.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType<TenantSummaryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TenantSummaryDto>> Create(
        [FromHeader(Name = "X-Platform-Key")] string? platformKey,
        CreateTenantRequest request,
        CancellationToken ct)
    {
        var expected = configuration["Platform:ProvisioningKey"];

        // No key configured means provisioning is closed, not open. Failing the
        // other way would leave a fresh deployment able to be filled with tenants
        // by anyone who found the endpoint.
        if (string.IsNullOrWhiteSpace(expected) || platformKey != expected)
        {
            return Unauthorized();
        }

        var tenant = await tenants.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetByCode), new { code = tenant.Code }, tenant);
    }
}
