namespace FleetRental.Application.Analytics;

/// <summary>
/// Turns one category's monthly demand history into a forecast and a plain-English
/// verdict on where it is heading. Pure — takes a series in, gives a DTO back — so
/// the trend rules unit-test without a database, the same split as
/// <see cref="SeriesForecaster"/> and <see cref="BookingApprovalModel"/>.
/// </summary>
public static class CategoryDemandAnalyzer
{
    /// <summary>How many trailing months the forecast is compared against.</summary>
    public const int TrendComparisonMonths = 3;

    /// <summary>
    /// Relative change below which demand is called steady rather than moving.
    /// Without this dead band, ordinary month-to-month noise would flip the label
    /// on every refresh and the word "rising" would stop meaning anything.
    /// </summary>
    public const double TrendSignificanceThreshold = 0.10;

    /// <summary>
    /// Forecasts <paramref name="horizonMonths"/> ahead and classifies the trend.
    /// Reports <see cref="CategoryDemandDto.HasSufficientHistory"/> false — with an
    /// empty forecast and <see cref="DemandTrend.Unknown"/> — when the series is too
    /// short to model, rather than presenting a guess as a projection.
    /// </summary>
    public static CategoryDemandDto Analyze(
        string category,
        int carCount,
        IReadOnlyList<DemandPointDto> history,
        DateOnly forecastStart,
        int horizonMonths)
    {
        var forecast = SeriesForecaster.Run([.. history.Select(h => (float)h.BookedDays)], horizonMonths);
        var recentAverage = RecentAverage(history);

        if (!forecast.HasSufficientHistory)
        {
            return new CategoryDemandDto
            {
                Category = category,
                CarCount = carCount,
                HasSufficientHistory = false,
                History = history,
                Forecast = [],
                Trend = DemandTrend.Unknown,
                RecentMonthlyAverage = Math.Round(recentAverage, 1),
                ForecastMonthlyAverage = 0,
            };
        }

        var forecastPoints = new List<DemandPointDto>(horizonMonths);
        var cursor = forecastStart;
        for (var i = 0; i < horizonMonths; i++)
        {
            forecastPoints.Add(new DemandPointDto
            {
                PeriodLabel = cursor.ToString("MMM yyyy"),
                PeriodStart = cursor,
                // SSA will happily project below zero on a declining series; a
                // negative number of booked days is not a thing.
                BookedDays = Math.Round(Math.Max(0, forecast.Values[i]), 1),
            });
            cursor = cursor.AddMonths(1);
        }

        var forecastAverage = forecastPoints.Average(p => p.BookedDays);

        return new CategoryDemandDto
        {
            Category = category,
            CarCount = carCount,
            HasSufficientHistory = true,
            History = history,
            Forecast = forecastPoints,
            Trend = ClassifyTrend(recentAverage, forecastAverage),
            RecentMonthlyAverage = Math.Round(recentAverage, 1),
            ForecastMonthlyAverage = Math.Round(forecastAverage, 1),
        };
    }

    /// <summary>
    /// A category the fleet owns cars in but has no settled demand for. Listed
    /// rather than omitted, so an owner can see they are holding capacity nobody
    /// has booked — that absence is itself the finding.
    /// </summary>
    public static CategoryDemandDto NoHistory(string category, int carCount) => new()
    {
        Category = category,
        CarCount = carCount,
        HasSufficientHistory = false,
        History = [],
        Forecast = [],
        Trend = DemandTrend.Unknown,
        RecentMonthlyAverage = 0,
        ForecastMonthlyAverage = 0,
    };

    /// <summary>The trailing months the forecast is judged against, not the whole history.</summary>
    public static double RecentAverage(IReadOnlyList<DemandPointDto> history) =>
        history.Count == 0 ? 0 : history.TakeLast(TrendComparisonMonths).Average(h => h.BookedDays);

    /// <summary>
    /// Rising or declining only when the forecast differs from recent months by more
    /// than <see cref="TrendSignificanceThreshold"/>; steady otherwise.
    /// </summary>
    public static DemandTrend ClassifyTrend(double recentAverage, double forecastAverage)
    {
        if (recentAverage <= 0)
        {
            // Nothing to take a percentage of: any forecast above zero is a genuine
            // pickup from a standing start, and zero-to-zero is simply steady.
            return forecastAverage > 0 ? DemandTrend.Rising : DemandTrend.Steady;
        }

        var change = (forecastAverage - recentAverage) / recentAverage;

        return change > TrendSignificanceThreshold ? DemandTrend.Rising
            : change < -TrendSignificanceThreshold ? DemandTrend.Declining
            : DemandTrend.Steady;
    }
}
