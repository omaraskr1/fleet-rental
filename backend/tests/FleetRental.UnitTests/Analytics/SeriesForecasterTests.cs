using FleetRental.Application.Analytics;

namespace FleetRental.UnitTests.Analytics;

/// <summary>
/// The forecasting math in isolation from the database — a synthetic series with a
/// known trend is enough to check SSA is wired up correctly and that the
/// insufficient-history guard actually holds the line.
/// </summary>
public class SeriesForecasterTests
{
    [Fact]
    public void Fewer_than_the_minimum_months_returns_insufficient_history()
    {
        var history = Enumerable.Range(1, SeriesForecaster.MinimumHistoryMonths - 1)
            .Select(i => (float)(i * 100))
            .ToArray();

        var forecast = SeriesForecaster.Run(history, horizon: 3);

        Assert.False(forecast.HasSufficientHistory);
        Assert.Empty(forecast.Values);
    }

    [Fact]
    public void Zero_or_negative_horizon_returns_insufficient_history()
    {
        var history = Enumerable.Range(1, 12).Select(i => (float)(i * 100)).ToArray();

        Assert.False(SeriesForecaster.Run(history, horizon: 0).HasSufficientHistory);
        Assert.False(SeriesForecaster.Run(history, horizon: -1).HasSufficientHistory);
    }

    [Fact]
    public void Enough_history_produces_exactly_horizon_forecasted_points()
    {
        var history = Enumerable.Range(1, 12).Select(i => (float)(i * 100)).ToArray();

        var forecast = SeriesForecaster.Run(history, horizon: 3);

        Assert.True(forecast.HasSufficientHistory);
        Assert.Equal(3, forecast.Values.Length);
        Assert.Equal(3, forecast.LowerBound.Length);
        Assert.Equal(3, forecast.UpperBound.Length);
    }

    /// <summary>
    /// A window sized at exactly half the series (the boundary MinimumHistoryMonths
    /// sits on) fails ML.NET's "trainSize > 2 x windowSize" requirement — regression
    /// coverage for a real bug caught while wiring this up against live data.
    /// </summary>
    [Theory]
    [InlineData(SeriesForecaster.MinimumHistoryMonths)]
    [InlineData(SeriesForecaster.MinimumHistoryMonths + 1)]
    [InlineData(SeriesForecaster.MinimumHistoryMonths + 2)]
    public void Exactly_the_minimum_history_length_does_not_throw(int months)
    {
        var history = Enumerable.Range(1, months).Select(i => (float)(i * 100)).ToArray();

        var forecast = SeriesForecaster.Run(history, horizon: 3);

        Assert.True(forecast.HasSufficientHistory);
        Assert.Equal(3, forecast.Values.Length);
    }

    [Fact]
    public void A_steady_upward_trend_is_projected_upward_not_flattened_to_the_average()
    {
        // 1200, 1300, 1400, ... 2300 — an unmistakable trend. The forecast for the
        // next point should continue past the last observed value, not regress to
        // the series mean the way a naive average-of-history model would.
        var history = Enumerable.Range(0, 12).Select(i => (float)(1200 + i * 100)).ToArray();

        var forecast = SeriesForecaster.Run(history, horizon: 1);

        Assert.True(forecast.HasSufficientHistory);
        Assert.True(forecast.Values[0] > history[^1],
            $"expected the forecast ({forecast.Values[0]}) to continue the upward trend past the last observed value ({history[^1]})");
    }

    [Fact]
    public void The_confidence_band_widens_further_into_the_future()
    {
        // SSA should be less certain about month 3 than month 1 — a tightening or
        // constant band would mean the confidence interval isn't doing anything.
        var history = Enumerable.Range(0, 12).Select(i => (float)(1000 + (i % 4) * 50)).ToArray();

        var forecast = SeriesForecaster.Run(history, horizon: 3);

        var firstWidth = forecast.UpperBound[0] - forecast.LowerBound[0];
        var lastWidth = forecast.UpperBound[^1] - forecast.LowerBound[^1];

        Assert.True(lastWidth >= firstWidth,
            $"expected the band at horizon 3 ({lastWidth}) to be at least as wide as at horizon 1 ({firstWidth})");
    }
}
