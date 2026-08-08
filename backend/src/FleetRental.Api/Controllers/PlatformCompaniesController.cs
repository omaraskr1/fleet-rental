using FleetRental.Application.Common;
using FleetRental.Application.Platform;
using FleetRental.Application.Tenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetRental.Api.Controllers;

[ApiController]
[Route("api/platform/companies")]
[Authorize(Roles = PlatformRoles.PlatformAdmin)]
public class PlatformCompaniesController(PlatformCompanyService companies) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CompanyDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CompanyDto>>> GetAll(CancellationToken ct) =>
        Ok(await companies.ListAsync(ct));

    [HttpPost]
    [ProducesResponseType<CompanyDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CompanyDto>> Create(CreateTenantRequest request, CancellationToken ct)
    {
        var company = await companies.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), company);
    }

    [HttpPost("{tenantId:guid}/suspend")]
    [ProducesResponseType<CompanyDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CompanyDto>> Suspend(Guid tenantId, CancellationToken ct) =>
        Ok(await companies.SuspendAsync(tenantId, ct));

    [HttpPost("{tenantId:guid}/reactivate")]
    [ProducesResponseType<CompanyDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CompanyDto>> Reactivate(Guid tenantId, CancellationToken ct) =>
        Ok(await companies.ReactivateAsync(tenantId, ct));

    [HttpGet("{tenantId:guid}/admins")]
    [ProducesResponseType<IReadOnlyList<CompanyAdminDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CompanyAdminDto>>> GetAdmins(Guid tenantId, CancellationToken ct) =>
        Ok(await companies.ListAdminsAsync(tenantId, ct));

    [HttpPost("{tenantId:guid}/admins")]
    [ProducesResponseType<CompanyAdminDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CompanyAdminDto>> CreateAdmin(
        Guid tenantId,
        CreateCompanyAdminRequest request,
        CancellationToken ct)
    {
        var admin = await companies.CreateAdminAsync(tenantId, request, ct);
        return CreatedAtAction(nameof(GetAdmins), new { tenantId }, admin);
    }
}
