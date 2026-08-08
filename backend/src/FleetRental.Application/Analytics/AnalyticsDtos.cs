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
/// are fewer than <see cref="RevenueForecaster.MinimumHistoryMonths"/> complete months on
/// file — the caller must check it before showing <see cref="Forecast"/>, which is empty
/// in that case rather than a guess dressed up as a number.
/// </summary>
public sealed record RevenueForecastDto
{
    public required bool HasSufficientHistory { get; init; }

    public required IReadOnlyList<RevenuePointDto> History { get; init; }

    public required IReadOnlyList<RevenueForecastPointDto> Forecast { get; init; }
}

/// <summary>
/// Whether a car is earning its keep. Not a model's judgement call — a plain
/// aggregation of numbers the fleet already tracks, ranked so the owner sees
/// the worst performer first without doing the subtraction themselves.
/// </summary>
public enum CarProfitabilityRecommendation
{
    Keep,

    /// <summary>Profitable, but barely used — capital tied up in a car that mostly sits idle.</summary>
    Review,

    /// <summary>Cost more to maintain than it earned in the range — a losing proposition as-is.</summary>
    ConsiderRetiring,
}

/// <summary>Revenue against maintenance cost, per car, for deciding which vehicles are worth keeping.</summary>
public sealed record CarProfitabilityDto
{
    public required Guid CarId { get; init; }

    public required string CarName { get; init; }

    public required decimal EstimatedRevenue { get; init; }

    public required decimal MaintenanceCost { get; init; }

    public required decimal NetProfit { get; init; }

    /// <summary>Null when there was no revenue to take a percentage of, rather than a misleading 0%.</summary>
    public double? ProfitMarginPercent { get; init; }

    public required double UtilizationPercent { get; init; }

    public required int BookingCount { get; init; }

    public required CarProfitabilityRecommendation Recommendation { get; init; }
}

/// <summary>How likely this fleet's own past decisions say a pending request is to be approved.</summary>
public sealed record BookingApprovalPredictionDto
{
    public required Guid BookingId { get; init; }

    /// <summary>Calibrated 0..1 — "roughly this share of similar past requests were approved".</summary>
    public required double ApprovalProbability { get; init; }
}

/// <summary>
/// Predictions for every pending request. <see cref="HasSufficientData"/> is false
/// until the fleet has decided enough bookings — and enough of them both ways — for
/// there to be a pattern worth imitating; <see cref="Predictions"/> is empty in that
/// case rather than filled with noise. <see cref="TrainedOnBookings"/> is surfaced so
/// the UI can say *why* there is nothing to show yet.
/// </summary>
public sealed record BookingApprovalPredictionsDto
{
    public required bool HasSufficientData { get; init; }

    public required int TrainedOnBookings { get; init; }

    public required int MinimumRequired { get; init; }

    public required IReadOnlyList<BookingApprovalPredictionDto> Predictions { get; init; }
}
