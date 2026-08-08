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
/// Revenue is estimated from <c>Car.Rate</c>, clamped to the requested range.
/// A per-day car earns <c>Rate</c> for every overlapping day; a per-event car
/// earns <c>Rate</c> exactly once per booking that overlaps at all, regardless
/// of how many of its days fall inside the range — see
/// <see cref="EstimateRevenue"/>. Phase 1 takes no payment and <c>Car.Rate</c>
/// "is shown for information only and is not used to compute a total anywhere"
/// (see <c>Car.cs</c>) — this service is the first thing that treats it as a
/// number, and every DTO name says "Estimated" so nothing downstream mistakes
/// it for a billed amount.
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
            .Select(b => new BookingRow(b.Status, b.Period.Start, b.Period.End, b.Car.Rate, b.Car.PricingModel))
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
            .Sum(b => EstimateRevenue(b.PricingModel, b.Rate, OverlapDays(b.Start, b.End, from, to)));

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
            .Select(b => new { b.Period.Start, b.Period.End, b.Car.Rate, b.Car.PricingModel })
            .ToListAsync(cancellationToken);

        return [.. EnumerateMonths(from, to).Select(monthStart =>
        {
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var inMonth = bookings.Where(b => b.Start <= monthEnd && b.End >= monthStart).ToList();

            return new RevenuePointDto
            {
                PeriodLabel = monthStart.ToString("MMM yyyy"),
                PeriodStart = monthStart,
                EstimatedRevenue = inMonth.Sum(b =>
                    EstimateRevenue(b.PricingModel, b.Rate, OverlapDays(b.Start, b.End, monthStart, monthEnd))),
                ApprovedBookings = inMonth.Count,
            };
        })];
    }

    /// <summary>
    /// Projects <paramref name="horizonMonths"/> months of revenue past today, from an
    /// SSA model trained on every fully-elapsed calendar month since the fleet's first
    /// booking. The current, still-accruing month is deliberately excluded from
    /// training — it would look like a demand collapse to the model every time this
    /// runs before the month is over.
    /// </summary>
    public async Task<RevenueForecastDto> GetRevenueForecastAsync(
        int horizonMonths = 3, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var historyEnd = currentMonthStart.AddDays(-1);

        var earliestBookingStart = await db.Bookings
            .OrderBy(b => b.Period.Start)
            .Select(b => (DateOnly?)b.Period.Start)
            .FirstOrDefaultAsync(cancellationToken);

        // Nothing has been booked yet, or the fleet's whole history sits inside the
        // still-open current month — there is no settled month to train on at all.
        if (earliestBookingStart is null || earliestBookingStart.Value > historyEnd)
        {
            return new RevenueForecastDto { HasSufficientHistory = false, History = [], Forecast = [] };
        }

        var historyStart = new DateOnly(earliestBookingStart.Value.Year, earliestBookingStart.Value.Month, 1);
        var history = await GetRevenueTrendAsync(historyStart, historyEnd, cancellationToken);

        var forecast = SeriesForecaster.Run([.. history.Select(h => (float)h.EstimatedRevenue)], horizonMonths);

        if (!forecast.HasSufficientHistory)
        {
            return new RevenueForecastDto { HasSufficientHistory = false, History = history, Forecast = [] };
        }

        var forecastPoints = new List<RevenueForecastPointDto>();
        var cursor = currentMonthStart;
        for (var i = 0; i < horizonMonths; i++)
        {
            forecastPoints.Add(new RevenueForecastPointDto
            {
                PeriodLabel = cursor.ToString("MMM yyyy"),
                PeriodStart = cursor,
                ForecastedRevenue = ToNonNegativeDecimal(forecast.Values[i]),
                LowerBound = ToNonNegativeDecimal(forecast.LowerBound[i]),
                UpperBound = ToNonNegativeDecimal(forecast.UpperBound[i]),
            });
            cursor = cursor.AddMonths(1);
        }

        return new RevenueForecastDto { HasSufficientHistory = true, History = history, Forecast = forecastPoints };
    }

    /// <summary>
    /// Scores every pending request by how likely this fleet's own past decisions
    /// say it is to be approved, so the admin queue can lead with the obvious yeses
    /// and surface the unusual requests that actually need thought.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Trained fresh on each call rather than cached. At the capped
    /// <see cref="BookingApprovalModel.MaxTrainingRows"/> rows, logistic regression
    /// fits in a few tens of milliseconds, and an admin opens this screen a handful
    /// of times a day — so a cache would buy nothing while introducing two ways to
    /// be wrong: a stale model that ignores decisions just made, and a cache key
    /// that leaks one tenant's model to another. Revisit if training rows or request
    /// volume grow by orders of magnitude.
    /// </para>
    /// <para>
    /// Cancelled bookings are excluded from training: the client withdrew, so the
    /// admin never rendered a judgement, and treating that as a rejection would
    /// teach the model to predict client behaviour under the guise of predicting
    /// the admin's.
    /// </para>
    /// </remarks>
    public async Task<BookingApprovalPredictionsDto> GetBookingApprovalPredictionsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await db.Bookings
            .Select(b => new PredictionRow(
                b.Id,
                b.ClientId,
                b.CreatedAt,
                b.Status,
                b.Period.Start,
                b.Period.End,
                b.Event.Type,
                b.Event.ExpectedAttendance,
                b.Car.Category))
            .ToListAsync(cancellationToken);

        var priorsByBooking = BuildClientPriors(rows);

        var decided = rows
            .Where(r => r.Status is BookingStatus.Approved or BookingStatus.Rejected)
            .OrderByDescending(r => r.CreatedAt)
            .Take(BookingApprovalModel.MaxTrainingRows)
            .Select(r => ToFeatures(r, priorsByBooking[r.Id], r.Status == BookingStatus.Approved))
            .ToList();

        var pending = rows
            .Where(r => r.Status == BookingStatus.Pending)
            .Select(r => new BookingApprovalModel.PendingBooking(
                r.Id,
                ToFeatures(r, priorsByBooking[r.Id], approved: false)))
            .ToList();

        var result = BookingApprovalModel.Run(decided, pending);

        return new BookingApprovalPredictionsDto
        {
            HasSufficientData = result.HasSufficientData,
            TrainedOnBookings = result.TrainedOnRows,
            MinimumRequired = BookingApprovalModel.MinimumTrainingRows,
            Predictions = [.. result.ProbabilityByBookingId
                .Select(kvp => new BookingApprovalPredictionDto
                {
                    BookingId = kvp.Key,
                    ApprovalProbability = Math.Round(kvp.Value, 3),
                })
                .OrderByDescending(p => p.ApprovalProbability)],
        };
    }

    /// <summary>
    /// A client's booking count and cancellation rate <em>as they stood when each
    /// request was submitted</em>, not as they stand today.
    /// </summary>
    /// <remarks>
    /// This distinction is the whole ballgame. Using present-day totals would let a
    /// two-year-old training row carry information that did not exist when the admin
    /// decided it — the model would score well in training by reading the future and
    /// then underperform on the live queue, where only the past is available.
    /// </remarks>
    private static Dictionary<Guid, ClientPriors> BuildClientPriors(IReadOnlyCollection<PredictionRow> rows)
    {
        var priors = new Dictionary<Guid, ClientPriors>(rows.Count);

        foreach (var clientBookings in rows.GroupBy(r => r.ClientId))
        {
            var ordered = clientBookings.OrderBy(r => r.CreatedAt).ToList();
            var seen = 0;
            var cancelled = 0;

            foreach (var booking in ordered)
            {
                priors[booking.Id] = new ClientPriors(seen, seen == 0 ? 0f : (float)cancelled / seen);

                seen++;
                if (booking.Status == BookingStatus.Cancelled)
                {
                    cancelled++;
                }
            }
        }

        return priors;
    }

    private static BookingApprovalModel.BookingFeatures ToFeatures(
        PredictionRow row, ClientPriors priors, bool approved) => new()
        {
            // Clamped at zero: a booking created after its own start date would be
            // a data error, and a negative lead time would skew normalisation.
            LeadTimeDays = Math.Max(0, row.Start.DayNumber - DateOnly.FromDateTime(row.CreatedAt.UtcDateTime).DayNumber),
            TotalDays = row.End.DayNumber - row.Start.DayNumber + 1,
            ExpectedAttendance = row.ExpectedAttendance ?? 0,
            ClientPriorBookings = priors.BookingCount,
            ClientPriorCancellationRate = priors.CancellationRate,
            StartDayOfWeek = (int)row.Start.DayOfWeek,
            StartMonth = row.Start.Month,
            EventType = row.EventType.ToString(),
            CarCategory = row.CarCategory.ToString(),
            Approved = approved,
        };

    private sealed record ClientPriors(int BookingCount, float CancellationRate);

    private sealed record PredictionRow(
        Guid Id,
        Guid ClientId,
        DateTimeOffset CreatedAt,
        BookingStatus Status,
        DateOnly Start,
        DateOnly End,
        EventType EventType,
        int? ExpectedAttendance,
        CarCategory CarCategory);

    /// <summary>
    /// Forecasts demand per vehicle category, so a fleet owner can see which kinds
    /// of vehicle to buy more of and which are losing interest — the composition
    /// question, as opposed to the per-car keep-or-retire one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One SSA model per category rather than one over the fleet: a rising SUV
    /// trend and a collapsing convertible trend average out to "steady" if pooled,
    /// which is precisely the signal worth having.
    /// </para>
    /// <para>
    /// Only categories the fleet actually owns cars in are reported. Forecasting
    /// demand for a bus nobody owns would be arithmetically possible — bookings
    /// against it are always zero — and completely useless.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<CategoryDemandDto>> GetCategoryDemandAsync(
        int horizonMonths = 3, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        // Same reasoning as the revenue forecast: the still-accruing current month
        // would read as a demand collapse to a model trained mid-month.
        var historyEnd = currentMonthStart.AddDays(-1);

        var carCounts = await db.Cars
            .GroupBy(c => c.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Category, x => x.Count, cancellationToken);

        if (carCounts.Count == 0)
        {
            return [];
        }

        var bookings = await db.Bookings
            .Where(b => b.Status == BookingStatus.Approved && b.Period.Start <= historyEnd)
            .Select(b => new { b.Car.Category, b.Period.Start, b.Period.End })
            .ToListAsync(cancellationToken);

        var earliest = bookings.Count == 0 ? (DateOnly?)null : bookings.Min(b => b.Start);
        if (earliest is null)
        {
            // No settled demand anywhere in the fleet: report the categories with
            // their capacity so the screen still lists them, but claim nothing.
            return [.. carCounts
                .Select(c => CategoryDemandAnalyzer.NoHistory(c.Key.ToString(), c.Value))
                .OrderBy(d => d.Category)];
        }

        var historyStart = new DateOnly(earliest.Value.Year, earliest.Value.Month, 1);
        var months = EnumerateMonths(historyStart, historyEnd).ToList();

        var result = carCounts
            .Select(entry => CategoryDemandAnalyzer.Analyze(
                entry.Key.ToString(),
                entry.Value,
                MonthlyDemand(months, [.. bookings.Where(b => b.Category == entry.Key).Select(b => (b.Start, b.End))]),
                currentMonthStart,
                horizonMonths))
            .OrderByDescending(d => d.ForecastMonthlyAverage)
            .ThenBy(d => d.Category);

        return [.. result];
    }

    /// <summary>Booked car-days per month for one category's bookings.</summary>
    private static List<DemandPointDto> MonthlyDemand(
        IReadOnlyList<DateOnly> months,
        IReadOnlyList<(DateOnly Start, DateOnly End)> bookings) =>
        [.. months.Select(monthStart =>
        {
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            return new DemandPointDto
            {
                PeriodLabel = monthStart.ToString("MMM yyyy"),
                PeriodStart = monthStart,
                BookedDays = bookings.Sum(b => OverlapDays(b.Start, b.End, monthStart, monthEnd)),
            };
        })];

    /// <summary>Demand per car — which vehicles carry the fleet and which sit idle.</summary>
    public async Task<IReadOnlyList<CarUtilizationDto>> GetUtilizationAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var daysInRange = to.DayNumber - from.DayNumber + 1;

        var cars = await db.Cars
            .Select(c => new { c.Id, c.Name, c.Rate, c.PricingModel })
            .ToListAsync(cancellationToken);

        var bookings = await db.Bookings
            .Where(b => b.Status == BookingStatus.Approved && b.Period.Start <= to && b.Period.End >= from)
            .Select(b => new { b.CarId, b.Period.Start, b.Period.End })
            .ToListAsync(cancellationToken);

        var result = cars.Select(car =>
        {
            var carBookings = bookings.Where(b => b.CarId == car.Id).ToList();
            var bookedDays = carBookings.Sum(b => OverlapDays(b.Start, b.End, from, to));

            // Revenue is summed per booking, not from the aggregate bookedDays —
            // a per-event car with two bookings in range earns Rate twice, not
            // once for their combined day count.
            var estimatedRevenue = carBookings.Sum(b =>
                EstimateRevenue(car.PricingModel, car.Rate, OverlapDays(b.Start, b.End, from, to)));

            return new CarUtilizationDto
            {
                CarId = car.Id,
                CarName = car.Name,
                BookedDays = bookedDays,
                DaysInRange = daysInRange,
                UtilizationPercent = daysInRange == 0 ? 0d : Math.Round(100.0 * bookedDays / daysInRange, 1),
                BookingCount = carBookings.Count,
                EstimatedRevenue = estimatedRevenue,
            };
        });

        return [.. result.OrderByDescending(r => r.UtilizationPercent)];
    }

    /// <summary>
    /// Whether each car is earning its keep: estimated revenue against what was
    /// actually spent maintaining it, worst net first so the vehicle most in need
    /// of a decision is the one the owner sees at the top.
    /// </summary>
    /// <remarks>
    /// Only the cost side is real money. Revenue remains an estimate (see
    /// <see cref="EstimateRevenue"/>) until payments are recorded, so "profit"
    /// here is directional — good for ranking one car against another, not for
    /// the books. Every field name says "Estimated" for that reason.
    /// </remarks>
    public async Task<IReadOnlyList<CarProfitabilityDto>> GetProfitabilityAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var utilization = await GetUtilizationAsync(from, to, cancellationToken);

        var maintenanceByCar = await db.ServiceRecords
            .Where(s => s.PerformedAt >= from && s.PerformedAt <= to)
            .GroupBy(s => s.CarId)
            .Select(g => new { CarId = g.Key, TotalCost = g.Sum(s => s.Cost) })
            .ToDictionaryAsync(x => x.CarId, x => x.TotalCost, cancellationToken);

        var result = utilization.Select(car =>
        {
            var maintenanceCost = maintenanceByCar.GetValueOrDefault(car.CarId);
            var netProfit = car.EstimatedRevenue - maintenanceCost;

            return new CarProfitabilityDto
            {
                CarId = car.CarId,
                CarName = car.CarName,
                EstimatedRevenue = car.EstimatedRevenue,
                MaintenanceCost = maintenanceCost,
                NetProfit = netProfit,
                // A percentage of nothing is not zero, it is undefined — a car that
                // earned nothing and cost nothing should not read as "0% margin"
                // alongside one that genuinely broke even on real money.
                ProfitMarginPercent = car.EstimatedRevenue == 0
                    ? null
                    : Math.Round((double)(netProfit / car.EstimatedRevenue) * 100, 1),
                UtilizationPercent = car.UtilizationPercent,
                BookingCount = car.BookingCount,
                Recommendation = Recommend(netProfit, car.UtilizationPercent),
            };
        });

        return [.. result.OrderBy(r => r.NetProfit)];
    }

    /// <summary>
    /// Deliberately a plain rule, not a model. The thresholds are a starting point
    /// for a conversation with the owner, which is why the DTO carries the numbers
    /// that produced them rather than the verdict alone.
    /// </summary>
    private static CarProfitabilityRecommendation Recommend(decimal netProfit, double utilizationPercent) =>
        netProfit < 0 ? CarProfitabilityRecommendation.ConsiderRetiring
        : utilizationPercent < IdleUtilizationThresholdPercent ? CarProfitabilityRecommendation.Review
        : CarProfitabilityRecommendation.Keep;

    /// <summary>Below this, a car is earning something but mostly sitting still.</summary>
    private const double IdleUtilizationThresholdPercent = 10.0;

    /// <summary>What kind of occasion books this fleet, and how much of that demand converts.</summary>
    public async Task<IReadOnlyList<EventTypeBreakdownDto>> GetEventTypeBreakdownAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var rows = await db.Bookings
            .Where(b => b.Period.Start <= to && b.Period.End >= from)
            .Select(b => new { b.Event.Type, b.Status, b.Period.Start, b.Period.End, b.Car.Rate, b.Car.PricingModel })
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
                    .Sum(r => EstimateRevenue(r.PricingModel, r.Rate, OverlapDays(r.Start, r.End, from, to))),
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

    /// <summary>SSA's confidence band can dip below zero for a near-flat series; revenue never does.</summary>
    private static decimal ToNonNegativeDecimal(float value) => (decimal)Math.Max(0f, value);

    /// <summary>
    /// A per-day car earns <paramref name="rate"/> for every overlapping day. A
    /// per-event car earns <paramref name="rate"/> exactly once for the booking,
    /// as long as it overlaps the range at all — a multi-day event priced per
    /// event is not multiplied by its length.
    /// </summary>
    private static decimal EstimateRevenue(PricingModel pricingModel, decimal rate, int overlapDays) =>
        pricingModel == PricingModel.PerEvent
            ? (overlapDays > 0 ? rate : 0m)
            : rate * overlapDays;

    private sealed record BookingRow(
        BookingStatus Status,
        DateOnly Start,
        DateOnly End,
        decimal Rate,
        PricingModel PricingModel);
}
