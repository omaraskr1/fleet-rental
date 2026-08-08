using FleetRental.Application.Abstractions;
using FleetRental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FleetRental.Application.Analytics;

/// <summary>
/// Read-only aggregation over bookings, cars, and maintenance data for the admin
/// dashboard. Deliberately introduces no new entities or storage — every figure
/// here is derived from data other features already collect, per the "Analytics"
/// entry under "Built for Phase 2 and 3" in the README: <c>EventType</c> is a
/// typed column and <c>BookedDays</c> gives per-day utilisation without parsing
/// ranges.
/// </summary>
/// <remarks>
/// Revenue is estimated as <c>Car.DailyRate × booked days</c>, clamped to the
/// requested range. Phase 1 takes no payment and <c>Car.DailyRate</c> "is shown
/// for information only and is not used to compute a total anywhere" (see
/// <c>Car.cs</c>) — this service is the first thing that treats it as a number,
/// and every DTO name says "Estimated" so nothing downstream mistakes it for a
/// billed amount.
/// </remarks>
public class AnalyticsService(IFleetRentalDbContext db)
{
    public async Task<AnalyticsOverviewDto> GetOverviewAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var totalCars = await db.Cars.CountAsync(cancellationToken);
        var activeCars = await db.Cars.CountAsync(c => c.Status == CarStatus.Active, cancellationToken);

        var bookings = await db.Bookings
            .Where(b => b.Period.Start <= to && b.Period.End >= from)
            .Select(b => new BookingRow(b.Status, b.Period.Start, b.Period.End, b.Car.DailyRate))
            .ToListAsync(cancellationToken);

        var pending = bookings.Count(b => b.Status == BookingStatus.Pending);
        var approved = bookings.Count(b => b.Status == BookingStatus.Approved);
        var rejected = bookings.Count(b => b.Status == BookingStatus.Rejected);
        var cancelled = bookings.Count(b => b.Status == BookingStatus.Cancelled);

        var decided = approved + rejected;
        var approvalRate = decided == 0 ? 0d : Math.Round(100.0 * approved / decided, 1);

        var approvedBookedDays = bookings
            .Where(b => b.Status == BookingStatus.Approved)
            .Sum(b => OverlapDays(b.Start, b.End, from, to));

        var estimatedRevenue = bookings
            .Where(b => b.Status == BookingStatus.Approved)
            .Sum(b => OverlapDays(b.Start, b.End, from, to) * b.DailyRate);

        var daysInRange = to.DayNumber - from.DayNumber + 1;
        var possibleCarDays = (long)activeCars * daysInRange;
        var utilization = possibleCarDays == 0 ? 0d : Math.Round(100.0 * approvedBookedDays / possibleCarDays, 1);

        var openIssues = await db.VehicleIssues
            .CountAsync(i => i.Status != IssueStatus.Resolved, cancellationToken);
        var criticalIssues = await db.VehicleIssues
            .CountAsync(i => i.Status != IssueStatus.Resolved && i.Severity == IssueSeverity.Critical, cancellationToken);

        var maintenanceCost = await db.ServiceRecords
            .Where(s => s.PerformedAt >= from && s.PerformedAt <= to)
            .SumAsync(s => s.Cost, cancellationToken);

        var carsServiceDue = await CountCarsServiceDueAsync(cancellationToken);

        return new AnalyticsOverviewDto
        {
            From = from,
            To = to,
            TotalCars = totalCars,
            ActiveCars = activeCars,
            TotalBookings = bookings.Count,
            PendingBookings = pending,
            ApprovedBookings = approved,
            RejectedBookings = rejected,
            CancelledBookings = cancelled,
            ApprovalRatePercent = approvalRate,
            EstimatedRevenue = estimatedRevenue,
            FleetUtilizationPercent = utilization,
            OpenIssueCount = openIssues,
            CriticalIssueCount = criticalIssues,
            CarsServiceDue = carsServiceDue,
            MaintenanceCost = maintenanceCost,
        };
    }

    /// <summary>One point per calendar month touching <paramref name="from"/>..<paramref name="to"/>.</summary>
    public async Task<IReadOnlyList<RevenuePointDto>> GetRevenueTrendAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var bookings = await db.Bookings
            .Where(b => b.Status == BookingStatus.Approved && b.Period.Start <= to && b.Period.End >= from)
            .Select(b => new { b.Period.Start, b.Period.End, b.Car.DailyRate })
            .ToListAsync(cancellationToken);

        return [.. EnumerateMonths(from, to).Select(monthStart =>
        {
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var inMonth = bookings.Where(b => b.Start <= monthEnd && b.End >= monthStart).ToList();

            return new RevenuePointDto
            {
                PeriodLabel = monthStart.ToString("MMM yyyy"),
                PeriodStart = monthStart,
                EstimatedRevenue = inMonth.Sum(b => OverlapDays(b.Start, b.End, monthStart, monthEnd) * b.DailyRate),
                ApprovedBookings = inMonth.Count,
            };
        })];
    }

    /// <summary>Demand per car — which vehicles carry the fleet and which sit idle.</summary>
    public async Task<IReadOnlyList<CarUtilizationDto>> GetUtilizationAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var daysInRange = to.DayNumber - from.DayNumber + 1;

        var cars = await db.Cars
            .Select(c => new { c.Id, c.Name, c.DailyRate })
            .ToListAsync(cancellationToken);

        var bookings = await db.Bookings
            .Where(b => b.Status == BookingStatus.Approved && b.Period.Start <= to && b.Period.End >= from)
            .Select(b => new { b.CarId, b.Period.Start, b.Period.End })
            .ToListAsync(cancellationToken);

        var result = cars.Select(car =>
        {
            var carBookings = bookings.Where(b => b.CarId == car.Id).ToList();
            var bookedDays = carBookings.Sum(b => OverlapDays(b.Start, b.End, from, to));

            return new CarUtilizationDto
            {
                CarId = car.Id,
                CarName = car.Name,
                BookedDays = bookedDays,
                DaysInRange = daysInRange,
                UtilizationPercent = daysInRange == 0 ? 0d : Math.Round(100.0 * bookedDays / daysInRange, 1),
                BookingCount = carBookings.Count,
                EstimatedRevenue = bookedDays * car.DailyRate,
            };
        });

        return [.. result.OrderByDescending(r => r.UtilizationPercent)];
    }

    /// <summary>What kind of occasion books this fleet, and how much of that demand converts.</summary>
    public async Task<IReadOnlyList<EventTypeBreakdownDto>> GetEventTypeBreakdownAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var rows = await db.Bookings
            .Where(b => b.Period.Start <= to && b.Period.End >= from)
            .Select(b => new { b.Event.Type, b.Status, b.Period.Start, b.Period.End, b.Car.DailyRate })
            .ToListAsync(cancellationToken);

        return [.. rows
            .GroupBy(r => r.Type)
            .Select(g => new EventTypeBreakdownDto
            {
                EventType = g.Key.ToString(),
                BookingCount = g.Count(),
                ApprovedCount = g.Count(r => r.Status == BookingStatus.Approved),
                EstimatedRevenue = g
                    .Where(r => r.Status == BookingStatus.Approved)
                    .Sum(r => OverlapDays(r.Start, r.End, from, to) * r.DailyRate),
            })
            .OrderByDescending(d => d.BookingCount)];
    }

    /// <summary>One point per calendar month, for tracking upkeep cost over time.</summary>
    public async Task<IReadOnlyList<MaintenanceCostPointDto>> GetMaintenanceCostTrendAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var records = await db.ServiceRecords
            .Where(s => s.PerformedAt >= from && s.PerformedAt <= to)
            .Select(s => new { s.PerformedAt, s.Cost })
            .ToListAsync(cancellationToken);

        return [.. EnumerateMonths(from, to).Select(monthStart =>
        {
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var inMonth = records.Where(r => r.PerformedAt >= monthStart && r.PerformedAt <= monthEnd).ToList();

            return new MaintenanceCostPointDto
            {
                PeriodLabel = monthStart.ToString("MMM yyyy"),
                PeriodStart = monthStart,
                TotalCost = inMonth.Sum(r => r.Cost),
                RecordCount = inMonth.Count,
            };
        })];
    }

    /// <summary>
    /// Mirrors the due calculation in <c>MaintenanceService.GetSummaryAsync</c>,
    /// batched across the whole fleet instead of one car at a time. A car opts in
    /// only by having both an odometer reading and a service interval set.
    /// </summary>
    private async Task<int> CountCarsServiceDueAsync(CancellationToken cancellationToken)
    {
        var candidates = await db.Cars
            .Where(c => c.CurrentOdometerKm != null && c.ServiceIntervalKm != null)
            .Select(c => new { c.Id, c.CurrentOdometerKm, c.ServiceIntervalKm })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return 0;
        }

        var carIds = candidates.Select(c => c.Id).ToList();

        var lastOdometerByCar = await db.ServiceRecords
            .Where(s => carIds.Contains(s.CarId) && s.OdometerKm != null)
            .GroupBy(s => s.CarId)
            .Select(g => new
            {
                CarId = g.Key,
                LastOdometerKm = g.OrderByDescending(s => s.PerformedAt).First().OdometerKm,
            })
            .ToDictionaryAsync(x => x.CarId, x => x.LastOdometerKm, cancellationToken);

        return candidates.Count(c =>
        {
            var lastOdometer = lastOdometerByCar.GetValueOrDefault(c.Id) ?? 0;
            var kmSinceLastService = c.CurrentOdometerKm!.Value - lastOdometer;
            return kmSinceLastService >= c.ServiceIntervalKm!.Value;
        });
    }

    /// <summary>Days a [start, end] range shares with [rangeFrom, rangeTo], both inclusive.</summary>
    private static int OverlapDays(DateOnly start, DateOnly end, DateOnly rangeFrom, DateOnly rangeTo)
    {
        var effectiveStart = start > rangeFrom ? start : rangeFrom;
        var effectiveEnd = end < rangeTo ? end : rangeTo;
        return effectiveEnd < effectiveStart ? 0 : effectiveEnd.DayNumber - effectiveStart.DayNumber + 1;
    }

    /// <summary>The first of each calendar month that <paramref name="from"/>..<paramref name="to"/> touches.</summary>
    private static IEnumerable<DateOnly> EnumerateMonths(DateOnly from, DateOnly to)
    {
        var cursor = new DateOnly(from.Year, from.Month, 1);
        var last = new DateOnly(to.Year, to.Month, 1);
        while (cursor <= last)
        {
            yield return cursor;
            cursor = cursor.AddMonths(1);
        }
    }

    private sealed record BookingRow(BookingStatus Status, DateOnly Start, DateOnly End, decimal DailyRate);
}
