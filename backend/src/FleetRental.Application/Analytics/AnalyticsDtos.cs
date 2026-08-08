namespace FleetRental.Application.Analytics;

/// <summary>
/// Fleet-wide snapshot for one date range. Revenue figures here are estimated
/// from <c>Car.DailyRate × booked days</c> — Phase 1 takes no payment, so there
/// is no billed amount to report instead. See <see cref="AnalyticsService"/>.
/// </summary>
public sealed record AnalyticsOverviewDto
{
    public required DateOnly From { get; init; }

    public required DateOnly To { get; init; }

    public required int TotalCars { get; init; }

    public required int ActiveCars { get; init; }

    public required int TotalBookings { get; init; }

    public required int PendingBookings { get; init; }

    public required int ApprovedBookings { get; init; }

    public required int RejectedBookings { get; init; }

    public required int CancelledBookings { get; init; }

    /// <summary>Approved ÷ (approved + rejected). Pending requests are excluded — they have no decision yet.</summary>
    public required double ApprovalRatePercent { get; init; }

    public required decimal EstimatedRevenue { get; init; }

    /// <summary>Approved car-days in range ÷ (active cars × days in range).</summary>
    public required double FleetUtilizationPercent { get; init; }

    public required int OpenIssueCount { get; init; }

    public required int CriticalIssueCount { get; init; }

    public required int CarsServiceDue { get; init; }

    public required decimal MaintenanceCost { get; init; }
}

/// <summary>One month's worth of estimated revenue, for the trend chart.</summary>
public sealed record RevenuePointDto
{
    public required string PeriodLabel { get; init; }

    public required DateOnly PeriodStart { get; init; }

    public required decimal EstimatedRevenue { get; init; }

    public required int ApprovedBookings { get; init; }
}

/// <summary>Per-car demand, for spotting which vehicles carry the fleet and which sit idle.</summary>
public sealed record CarUtilizationDto
{
    public required Guid CarId { get; init; }

    public required string CarName { get; init; }

    public required int BookedDays { get; init; }

    public required int DaysInRange { get; init; }

    public required double UtilizationPercent { get; init; }

    public required int BookingCount { get; init; }

    public required decimal EstimatedRevenue { get; init; }
}

/// <summary>Demand and revenue grouped by <c>EventType</c> — what kind of occasion books this fleet.</summary>
public sealed record EventTypeBreakdownDto
{
    public required string EventType { get; init; }

    public required int BookingCount { get; init; }

    public required int ApprovedCount { get; init; }

    public required decimal EstimatedRevenue { get; init; }
}

/// <summary>One month's total spend on service records, for tracking upkeep cost over time.</summary>
public sealed record MaintenanceCostPointDto
{
    public required string PeriodLabel { get; init; }

    public required DateOnly PeriodStart { get; init; }

    public required decimal TotalCost { get; init; }

    public required int RecordCount { get; init; }
}

/// <summary>One forecasted month, with the 95% confidence band SSA reports alongside its point estimate.</summary>
public sealed record RevenueForecastPointDto
{
    public required string PeriodLabel { get; init; }

    public required DateOnly PeriodStart { get; init; }

    public required decimal ForecastedRevenue { get; init; }

    public required decimal LowerBound { get; init; }

    public required decimal UpperBound { get; init; }
}

/// <summary>
/// History plus projection. <see cref="HasSufficientHistory"/> is false whenever there
/// are fewer than <see cref="SeriesForecaster.MinimumHistoryMonths"/> complete months on
/// file — the caller must check it before showing <see cref="Forecast"/>, which is empty
/// in that case rather than a guess dressed up as a number.
/// </summary>
public sealed record RevenueForecastDto
{
    public required bool HasSufficientHistory { get; init; }

    public required IReadOnlyList<RevenuePointDto> History { get; init; }

    public required IReadOnlyList<RevenueForecastPointDto> Forecast { get; init; }
}

/// <summary>Where demand for a category is heading, relative to how it has recently behaved.</summary>
public enum DemandTrend
{
    /// <summary>Not enough settled months to say — never presented as "steady".</summary>
    Unknown,

    Rising,

    Steady,

    Declining,
}

/// <summary>One month of demand for a category, measured in booked car-days.</summary>
public sealed record DemandPointDto
{
    public required string PeriodLabel { get; init; }

    public required DateOnly PeriodStart { get; init; }

    public required double BookedDays { get; init; }
}

/// <summary>
/// Forecast demand for one vehicle category, paired with how many cars of that
/// category the fleet currently owns — the two numbers together are what answer
/// "should I buy another one of these, or is one already sitting idle."
/// </summary>
/// <remarks>
/// Demand is measured in booked car-days rather than revenue on purpose: revenue
/// mixes demand with whatever the daily rate happens to be, so a category whose
/// price was raised would look like growing demand when nothing about the demand
/// changed.
/// </remarks>
public sealed record CategoryDemandDto
{
    public required string Category { get; init; }

    /// <summary>Cars of this category in the fleet today — the capacity side of the comparison.</summary>
    public required int CarCount { get; init; }

    public required bool HasSufficientHistory { get; init; }

    public required IReadOnlyList<DemandPointDto> History { get; init; }

    public required IReadOnlyList<DemandPointDto> Forecast { get; init; }

    public required DemandTrend Trend { get; init; }

    /// <summary>Mean booked days across the recent months the trend is judged against.</summary>
    public required double RecentMonthlyAverage { get; init; }

    /// <summary>Mean booked days across the forecast horizon. Zero when there is no forecast.</summary>
    public required double ForecastMonthlyAverage { get; init; }
}
