using Microsoft.ML;
using Microsoft.ML.Data;

namespace FleetRental.Application.Analytics;

/// <summary>
/// Predicts whether an admin will approve a pending booking, trained on the
/// decisions they have already made. Kept free of any database dependency — it
/// takes rows in and gives probabilities back — so the feature engineering and
/// the guards below unit-test without SQL Server, the same split as
/// <see cref="RevenueForecaster"/>.
/// </summary>
/// <remarks>
/// <para>
/// Labels cost nothing here: every approved or rejected booking is already a
/// labelled example, and the set grows on its own as the fleet operates. That is
/// what makes this worth modelling at all at this data scale.
/// </para>
/// <para>
/// The trainer is <c>LbfgsLogisticRegression</c> deliberately. It ships inside
/// the <c>Microsoft.ML</c> package (no extra dependency), it returns calibrated
/// probabilities rather than raw scores — so "0.82" genuinely means "about 82%
/// of requests that look like this were approved" — and with a handful of
/// features on a few hundred rows it is far less prone to memorising the
/// training set than a boosted tree would be.
/// </para>
/// </remarks>
public static class BookingApprovalModel
{
    /// <summary>
    /// Below this many decided bookings, the fleet has not shown the model enough
    /// of its own judgement to imitate it.
    /// </summary>
    public const int MinimumTrainingRows = 30;

    /// <summary>
    /// Both outcomes must appear at least this many times. A fleet that has
    /// approved all 200 of its requests has taught the model nothing except
    /// "always yes" — logistic regression on a single-class set produces a
    /// degenerate model that reports near-certainty on every input, which is
    /// worse than admitting there is nothing to predict.
    /// </summary>
    public const int MinimumRowsPerOutcome = 5;

    /// <summary>
    /// Caps how much history one training run reads. Recent decisions reflect how
    /// the fleet operates now, and a bound keeps the per-request training cost
    /// flat as a long-running tenant accumulates thousands of bookings.
    /// </summary>
    public const int MaxTrainingRows = 1000;

    public sealed record Result(bool HasSufficientData, int TrainedOnRows, IReadOnlyDictionary<Guid, double> ProbabilityByBookingId)
    {
        public static Result Insufficient(int rows) =>
            new(false, rows, new Dictionary<Guid, double>());
    }

    /// <summary>
    /// Trains on <paramref name="decided"/> and scores <paramref name="pending"/>.
    /// Returns <see cref="Result.Insufficient"/> — not a guess — whenever the
    /// training set is too small or too one-sided to learn anything from.
    /// </summary>
    public static Result Run(
        IReadOnlyCollection<BookingFeatures> decided,
        IReadOnlyCollection<PendingBooking> pending)
    {
        if (decided.Count < MinimumTrainingRows || !HasBothOutcomes(decided))
        {
            return Result.Insufficient(decided.Count);
        }

        if (pending.Count == 0)
        {
            return new Result(true, decided.Count, new Dictionary<Guid, double>());
        }

        var mlContext = new MLContext(seed: 1);
        var trainingData = mlContext.Data.LoadFromEnumerable(decided);

        var pipeline = mlContext.Transforms.Categorical
            .OneHotEncoding(nameof(BookingFeatures.EventType))
            .Append(mlContext.Transforms.Categorical.OneHotEncoding(nameof(BookingFeatures.CarCategory)))
            .Append(mlContext.Transforms.Concatenate(
                "Features",
                nameof(BookingFeatures.LeadTimeDays),
                nameof(BookingFeatures.TotalDays),
                nameof(BookingFeatures.ExpectedAttendance),
                nameof(BookingFeatures.ClientPriorBookings),
                nameof(BookingFeatures.ClientPriorCancellationRate),
                nameof(BookingFeatures.StartDayOfWeek),
                nameof(BookingFeatures.StartMonth),
                nameof(BookingFeatures.EventType),
                nameof(BookingFeatures.CarCategory)))
            // Lead time runs to hundreds while a cancellation rate sits between 0
            // and 1; without rescaling, L-BFGS lets the large-magnitude features
            // dominate the gradient regardless of how predictive they actually are.
            .Append(mlContext.Transforms.NormalizeMinMax("Features"))
            .Append(mlContext.BinaryClassification.Trainers.LbfgsLogisticRegression(
                labelColumnName: nameof(BookingFeatures.Approved),
                featureColumnName: "Features"));

        var model = pipeline.Fit(trainingData);
        var engine = mlContext.Model.CreatePredictionEngine<BookingFeatures, ApprovalPrediction>(model);

        var probabilities = new Dictionary<Guid, double>(pending.Count);
        foreach (var candidate in pending)
        {
            var prediction = engine.Predict(candidate.Features);
            probabilities[candidate.BookingId] = Math.Clamp(prediction.Probability, 0f, 1f);
        }

        return new Result(true, decided.Count, probabilities);
    }

    private static bool HasBothOutcomes(IReadOnlyCollection<BookingFeatures> rows)
    {
        var approved = rows.Count(r => r.Approved);
        return approved >= MinimumRowsPerOutcome && rows.Count - approved >= MinimumRowsPerOutcome;
    }

    /// <summary>A pending request awaiting a score, paired with the booking it belongs to.</summary>
    public sealed record PendingBooking(Guid BookingId, BookingFeatures Features);

    /// <summary>
    /// One training or scoring row. Everything here is knowable at the moment a
    /// request arrives — nothing derived from the decision itself, which would
    /// leak the answer into the features and produce a model that looks flawless
    /// in training and is useless in the queue.
    /// </summary>
    public sealed class BookingFeatures
    {
        /// <summary>Days between the request being submitted and the rental starting.</summary>
        public float LeadTimeDays { get; set; }

        public float TotalDays { get; set; }

        /// <summary>Zero when the client left it blank — the absence is itself a weak signal.</summary>
        public float ExpectedAttendance { get; set; }

        public float ClientPriorBookings { get; set; }

        /// <summary>0..1 across the client's earlier requests. Zero for a first-time client.</summary>
        public float ClientPriorCancellationRate { get; set; }

        public float StartDayOfWeek { get; set; }

        public float StartMonth { get; set; }

        public string EventType { get; set; } = string.Empty;

        public string CarCategory { get; set; } = string.Empty;

        /// <summary>The label. Ignored when scoring a pending request.</summary>
        public bool Approved { get; set; }
    }

    private sealed class ApprovalPrediction
    {
        [ColumnName("PredictedLabel")]
        public bool WillApprove { get; set; }

        public float Probability { get; set; }

        public float Score { get; set; }
    }
}
