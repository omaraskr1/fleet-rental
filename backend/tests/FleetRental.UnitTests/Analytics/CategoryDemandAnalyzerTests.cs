using FleetRental.Application.Analytics;

namespace FleetRental.UnitTests.Analytics;

/// <summary>
/// The demand trend rules in isolation from the database. These are the numbers a
/// fleet owner would act on when deciding what to buy, so the thresholds deserve
/// tests rather than trust.
/// </summary>
public class CategoryDemandAnalyzerTests
{
    private static readonly DateOnly ForecastStart = new(2027, 1, 1);

    private static List<DemandPointDto> Series(params double[] bookedDays) =>
        [.. bookedDays.Select((days, i) =>
        {
            var month = new DateOnly(2026, 1, 1).AddMonths(i);
            return new DemandPointDto
            {
                PeriodLabel = month.ToString("MMM yyyy"),
                PeriodStart = month,
                BookedDays = days,
            };
        })];

    // ---------- Trend classification ----------

    [Fact]
    public void A_forecast_well_above_recent_months_is_rising()
    {
        Assert.Equal(DemandTrend.Rising, CategoryDemandAnalyzer.ClassifyTrend(recentAverage: 10, forecastAverage: 15));
    }

    [Fact]
    public void A_forecast_well_below_recent_months_is_declining()
    {
        Assert.Equal(DemandTrend.Declining, CategoryDemandAnalyzer.ClassifyTrend(recentAverage: 10, forecastAverage: 5));
    }

    [Theory]
    [InlineData(10, 10)]      // unchanged
    [InlineData(10, 10.9)]    // +9%, inside the dead band
    [InlineData(10, 9.1)]     // -9%, inside the dead band
    public void Small_movements_are_steady_rather_than_a_trend(double recent, double forecast)
    {
        // Without the dead band, noise this size would flip the label on every
        // refresh and "rising" would stop carrying information.
        Assert.Equal(DemandTrend.Steady, CategoryDemandAnalyzer.ClassifyTrend(recent, forecast));
    }

    [Theory]
    [InlineData(10, 11.1)]
    [InlineData(10, 8.9)]
    public void Movements_past_the_threshold_are_a_trend(double recent, double forecast)
    {
        Assert.NotEqual(DemandTrend.Steady, CategoryDemandAnalyzer.ClassifyTrend(recent, forecast));
    }

    [Fact]
    public void Demand_appearing_from_nothing_is_rising_not_a_division_by_zero()
    {
        // A category with no recent bookings has no baseline to take a percentage
        // of; any forecast above zero is still a genuine pickup.
        Assert.Equal(DemandTrend.Rising, CategoryDemandAnalyzer.ClassifyTrend(recentAverage: 0, forecastAverage: 4));
    }

    [Fact]
    public void No_demand_and_no_forecast_is_steady_not_rising()
    {
        Assert.Equal(DemandTrend.Steady, CategoryDemandAnalyzer.ClassifyTrend(recentAverage: 0, forecastAverage: 0));
    }

    // ---------- Recent average ----------

    [Fact]
    public void The_recent_average_uses_only_the_trailing_comparison_window()
    {
        // Leading months are deliberately huge: if they leaked into the average the
        // result would be far above the trailing 3 months' value of 2.
        var history = Series(100, 100, 100, 100, 2, 2, 2);

        Assert.Equal(2, CategoryDemandAnalyzer.RecentAverage(history));
    }

    [Fact]
    public void An_empty_history_averages_to_zero_rather_than_throwing()
    {
        Assert.Equal(0, CategoryDemandAnalyzer.RecentAverage([]));
    }

    // ---------- Analyze ----------

    [Fact]
    public void Too_short_a_history_reports_unknown_rather_than_steady()
    {
        // "Unknown" and "Steady" must not be conflated: one means the fleet has no
        // idea yet, the other is a claim that demand is holding.
        var result = CategoryDemandAnalyzer.Analyze("Suv", carCount: 2, Series(4, 5, 3), ForecastStart, horizonMonths: 3);

        Assert.False(result.HasSufficientHistory);
        Assert.Equal(DemandTrend.Unknown, result.Trend);
        Assert.Empty(result.Forecast);
        Assert.Equal(2, result.CarCount);
    }

    [Fact]
    public void Enough_history_produces_a_forecast_point_per_horizon_month()
    {
        var result = CategoryDemandAnalyzer.Analyze(
            "Van", carCount: 3, Series(5, 6, 5, 7, 6, 8, 7, 9), ForecastStart, horizonMonths: 3);

        Assert.True(result.HasSufficientHistory);
        Assert.Equal(3, result.Forecast.Count);
        Assert.Equal("Jan 2027", result.Forecast[0].PeriodLabel);
        Assert.Equal("Mar 2027", result.Forecast[^1].PeriodLabel);
        Assert.NotEqual(DemandTrend.Unknown, result.Trend);
    }

    [Fact]
    public void A_forecast_never_reports_negative_booked_days()
    {
        // A steeply falling series will push SSA's projection below zero; negative
        // booked days are meaningless and must be clamped before display.
        var result = CategoryDemandAnalyzer.Analyze(
            "Bus", carCount: 1, Series(40, 34, 28, 22, 16, 10, 4, 1), ForecastStart, horizonMonths: 3);

        Assert.True(result.HasSufficientHistory);
        Assert.All(result.Forecast, p => Assert.True(p.BookedDays >= 0, $"got {p.BookedDays}"));
    }

    [Fact]
    public void A_collapsing_series_is_reported_as_declining()
    {
        var result = CategoryDemandAnalyzer.Analyze(
            "Convertible", carCount: 2, Series(30, 28, 25, 20, 14, 9, 5, 2), ForecastStart, horizonMonths: 3);

        Assert.Equal(DemandTrend.Declining, result.Trend);
    }

    [Fact]
    public void History_is_returned_alongside_the_forecast_for_charting()
    {
        var history = Series(5, 6, 5, 7, 6, 8);

        var result = CategoryDemandAnalyzer.Analyze("Sedan", carCount: 1, history, ForecastStart, horizonMonths: 2);

        Assert.Equal(history.Count, result.History.Count);
    }

    [Fact]
    public void A_category_with_no_history_still_reports_its_capacity()
    {
        // The point of listing it: an owner is holding cars nobody has booked, and
        // that absence is the finding rather than a reason to hide the row.
        var result = CategoryDemandAnalyzer.NoHistory("BrandedTruck", carCount: 4);

        Assert.Equal(4, result.CarCount);
        Assert.False(result.HasSufficientHistory);
        Assert.Equal(DemandTrend.Unknown, result.Trend);
        Assert.Empty(result.History);
        Assert.Empty(result.Forecast);
    }
}
