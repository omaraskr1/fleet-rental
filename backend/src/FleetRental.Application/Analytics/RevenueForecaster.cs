using Microsoft.ML;
using Microsoft.ML.Transforms.TimeSeries;

namespace FleetRental.Application.Analytics;

/// <summary>
/// Wraps ML.NET's SSA (Singular Spectrum Analysis) time-series forecaster behind a
/// plain <c>float[] in, float[] out</c> signature, kept separate from
/// <see cref="AnalyticsService"/> so the forecasting math can be unit-tested without
/// a database.
/// </summary>
/// <remarks>
/// SSA needs enough history to separate signal from noise — <see cref="MinimumHistoryMonths"/>
/// is the line between "forecast" and "guess", and callers must check
/// <see cref="Forecast.HasSufficientHistory"/> before showing anything to a user, the same way
/// <c>Car.ServiceIntervalKm</c> being unset means "not tracked", never "definitely fine."
/// </remarks>
public static class RevenueForecaster
{
    /// <summary>Fewer months than this and SSA has too little signal to separate from noise.</summary>
    public const int MinimumHistoryMonths = 6;

    public sealed record Forecast(bool HasSufficientHistory, float[] Values, float[] LowerBound, float[] UpperBound)
    {
        public static readonly Forecast Insufficient = new(false, [], [], []);
    }

    /// <summary>
    /// Forecasts <paramref name="horizon"/> future points from <paramref name="history"/>,
    /// oldest first. Returns <see cref="Forecast.Insufficient"/> when there is not enough
    /// history to model rather than extrapolating from noise.
    /// </summary>
    public static Forecast Run(float[] history, int horizon)
    {
        if (history.Length < MinimumHistoryMonths || horizon < 1)
        {
            return Forecast.Insufficient;
        }

        var mlContext = new MLContext(seed: 1);
        var dataView = mlContext.Data.LoadFromEnumerable(history.Select(v => new SeriesPoint { Value = v }));

        // SSA requires the training size to be strictly greater than twice the
        // window size, not just larger than the window itself — (length-1)/2 is
        // the largest window that leaves room for that, floor-divided so it never
        // lands exactly on the boundary. Capped at 4 because the seasonal cycle a
        // small fleet's demand would show (yearly) is far longer than any history
        // a fresh deployment has on file.
        var windowSize = Math.Clamp((history.Length - 1) / 2, 2, 4);

        var pipeline = mlContext.Forecasting.ForecastBySsa(
            outputColumnName: nameof(SsaPrediction.ForecastedValues),
            inputColumnName: nameof(SeriesPoint.Value),
            windowSize: windowSize,
            seriesLength: history.Length,
            trainSize: history.Length,
            horizon: horizon,
            confidenceLevel: 0.95f,
            confidenceLowerBoundColumn: nameof(SsaPrediction.LowerBoundValues),
            confidenceUpperBoundColumn: nameof(SsaPrediction.UpperBoundValues));

        var transformer = pipeline.Fit(dataView);
        using var engine = transformer.CreateTimeSeriesEngine<SeriesPoint, SsaPrediction>(mlContext);
        var prediction = engine.Predict();

        return new Forecast(true, prediction.ForecastedValues, prediction.LowerBoundValues, prediction.UpperBoundValues);
    }

    private sealed class SeriesPoint
    {
        public float Value { get; set; }
    }

    private sealed class SsaPrediction
    {
        public float[] ForecastedValues { get; set; } = [];

        public float[] LowerBoundValues { get; set; } = [];

        public float[] UpperBoundValues { get; set; } = [];
    }
}
