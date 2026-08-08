using FleetRental.Application.Analytics;
using FleetRental.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetRental.Api.Controllers;

/// <summary>
/// Fleet-wide analytics for the admin dashboard. Admin-only, like maintenance —
/// clients booking a car have no reason to see fleet revenue or utilisation.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("/api/analytics")]
public class AnalyticsController(AnalyticsService analytics) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType<AnalyticsOverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalyticsOverviewDto>> GetOverview(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var (f, t) = ResolveRange(from, to);
        return Ok(await analytics.GetOverviewAsync(f, t, ct));
    }

    [HttpGet("revenue")]
    [ProducesResponseType<IReadOnlyList<RevenuePointDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RevenuePointDto>>> GetRevenueTrend(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var (f, t) = ResolveRange(from, to);
        return Ok(await analytics.GetRevenueTrendAsync(f, t, ct));
    }

    [HttpGet("utilization")]
    [ProducesResponseType<IReadOnlyList<CarUtilizationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CarUtilizationDto>>> GetUtilization(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var (f, t) = ResolveRange(from, to);
        return Ok(await analytics.GetUtilizationAsync(f, t, ct));
    }

    [HttpGet("event-types")]
    [ProducesResponseType<IReadOnlyList<EventTypeBreakdownDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EventTypeBreakdownDto>>> GetEventTypeBreakdown(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var (f, t) = ResolveRange(from, to);
        return Ok(await analytics.GetEventTypeBreakdownAsync(f, t, ct));
    }

    [HttpGet("maintenance-costs")]
    [ProducesResponseType<IReadOnlyList<MaintenanceCostPointDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MaintenanceCostPointDto>>> GetMaintenanceCostTrend(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var (f, t) = ResolveRange(from, to);
        return Ok(await analytics.GetMaintenanceCostTrendAsync(f, t, ct));
    }

    [HttpGet("profitability")]
    [ProducesResponseType<IReadOnlyList<CarProfitabilityDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CarProfitabilityDto>>> GetProfitability(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var (f, t) = ResolveRange(from, to);
        return Ok(await analytics.GetProfitabilityAsync(f, t, ct));
    }

    /// <summary>Approval likelihood for every pending request, for sorting the admin queue.</summary>
    [HttpGet("booking-predictions")]
    [ProducesResponseType<BookingApprovalPredictionsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BookingApprovalPredictionsDto>> GetBookingApprovalPredictions(
        CancellationToken ct) =>
        Ok(await analytics.GetBookingApprovalPredictionsAsync(ct));

    /// <summary>Forecast demand per vehicle category, for fleet composition decisions.</summary>
    [HttpGet("category-demand")]
    [ProducesResponseType<IReadOnlyList<CategoryDemandDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDemandDto>>> GetCategoryDemand(
        [FromQuery] int months, CancellationToken ct) =>
        Ok(await analytics.GetCategoryDemandAsync(months <= 0 ? 3 : months, ct));

    [HttpGet("revenue-forecast")]
    [ProducesResponseType<RevenueForecastDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RevenueForecastDto>> GetRevenueForecast(
        [FromQuery] int months, CancellationToken ct) =>
        Ok(await analytics.GetRevenueForecastAsync(months <= 0 ? 3 : months, ct));

    /// <summary>
    /// Defaults to the trailing 12 months through 3 months out, when the caller
    /// supplies neither bound. Clients book ahead of their event, often weeks or
    /// months out, so a window ending today would hide the pipeline of already-
    /// approved future bookings — the 3-month lookahead keeps those in view
    /// alongside the trailing performance history.
    /// </summary>
    private static (DateOnly From, DateOnly To) ResolveRange(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var resolvedTo = to ?? today.AddMonths(3);
        var resolvedFrom = from ?? today.AddMonths(-12).AddDays(1 - today.Day);
        return (resolvedFrom, resolvedTo);
    }
}
