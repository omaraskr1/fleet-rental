using FleetRental.Api.Extensions;
using FleetRental.Application.Maintenance;
using FleetRental.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetRental.Api.Controllers;

/// <summary>
/// Mechanical history and issue tracking. Admin-only across the board — clients
/// booking a car have no reason to see its service log or open issues.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
public class MaintenanceController(MaintenanceService maintenance) : ControllerBase
{
    [HttpGet("/api/cars/{carId:guid}/maintenance")]
    [ProducesResponseType<CarMaintenanceSummaryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CarMaintenanceSummaryDto>> GetSummary(Guid carId, CancellationToken ct) =>
        Ok(await maintenance.GetSummaryAsync(carId, ct));

    [HttpGet("/api/cars/{carId:guid}/service-records")]
    [ProducesResponseType<IReadOnlyList<ServiceRecordDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ServiceRecordDto>>> GetServiceHistory(Guid carId, CancellationToken ct) =>
        Ok(await maintenance.GetServiceHistoryAsync(carId, ct));

    [HttpPost("/api/cars/{carId:guid}/service-records")]
    [ProducesResponseType<ServiceRecordDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ServiceRecordDto>> LogService(
        Guid carId,
        LogServiceRequest request,
        CancellationToken ct)
    {
        var record = await maintenance.LogServiceAsync(carId, request, ct);
        return CreatedAtAction(nameof(GetServiceHistory), new { carId }, record);
    }

    [HttpPut("/api/cars/{carId:guid}/odometer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateOdometer(Guid carId, UpdateOdometerRequest request, CancellationToken ct)
    {
        await maintenance.UpdateOdometerAsync(carId, request.Km, ct);
        return NoContent();
    }

    [HttpPut("/api/cars/{carId:guid}/service-interval")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetServiceInterval(
        Guid carId,
        SetServiceIntervalRequest request,
        CancellationToken ct)
    {
        await maintenance.SetServiceIntervalAsync(carId, request.Km, ct);
        return NoContent();
    }

    [HttpPost("/api/cars/{carId:guid}/issues")]
    [ProducesResponseType<VehicleIssueDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<VehicleIssueDto>> ReportIssue(
        Guid carId,
        ReportIssueRequest request,
        CancellationToken ct)
    {
        var issue = await maintenance.ReportIssueAsync(carId, User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(GetIssues), new { carId }, issue);
    }

    /// <summary>Fleet-wide by default, or scoped to one car — this is the owner's issue dashboard.</summary>
    [HttpGet("/api/issues")]
    [ProducesResponseType<IReadOnlyList<VehicleIssueDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VehicleIssueDto>>> GetIssues(
        [FromQuery] Guid? carId,
        [FromQuery] IssueStatus? status,
        CancellationToken ct) =>
        Ok(await maintenance.GetIssuesAsync(carId, status, ct));

    [HttpPost("/api/issues/{issueId:guid}/start-progress")]
    [ProducesResponseType<VehicleIssueDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<VehicleIssueDto>> StartProgress(Guid issueId, CancellationToken ct) =>
        Ok(await maintenance.StartProgressAsync(issueId, ct));

    [HttpPost("/api/issues/{issueId:guid}/resolve")]
    [ProducesResponseType<VehicleIssueDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<VehicleIssueDto>> Resolve(
        Guid issueId,
        ResolveIssueRequest request,
        CancellationToken ct) =>
        Ok(await maintenance.ResolveIssueAsync(issueId, request, ct));

    [HttpPost("/api/issues/{issueId:guid}/reopen")]
    [ProducesResponseType<VehicleIssueDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<VehicleIssueDto>> Reopen(Guid issueId, CancellationToken ct) =>
        Ok(await maintenance.ReopenIssueAsync(issueId, ct));
}
