using FleetRental.Application.Analytics;

namespace FleetRental.UnitTests.Analytics;

/// <summary>
/// The approval model in isolation from the database. Synthetic rows with a
/// deliberately planted pattern are enough to check the trainer is wired up
/// correctly and — more importantly — that the guards refuse to predict when
/// there is nothing to learn from.
/// </summary>
public class BookingApprovalModelTests
{
    /// <summary>
    /// Builds a row whose lead time is the only thing that varies, so a test can
    /// plant a pattern the model should be able to find.
    /// </summary>
    private static BookingApprovalModel.BookingFeatures Row(
        float leadTimeDays,
        bool approved,
        string eventType = "TradeShow",
        string carCategory = "Van") => new()
        {
            LeadTimeDays = leadTimeDays,
            TotalDays = 3,
            ExpectedAttendance = 200,
            ClientPriorBookings = 2,
            ClientPriorCancellationRate = 0f,
            StartDayOfWeek = 3,
            StartMonth = 6,
            EventType = eventType,
            CarCategory = carCategory,
            Approved = approved,
        };

    /// <summary>Long lead times get approved, last-minute ones get rejected.</summary>
    private static List<BookingApprovalModel.BookingFeatures> LearnableSet(int perOutcome = 20) =>
        [
            .. Enumerable.Range(0, perOutcome).Select(i => Row(60 + i, approved: true)),
            .. Enumerable.Range(0, perOutcome).Select(i => Row(1 + i % 3, approved: false)),
        ];

    // ---------- Guards ----------

    [Fact]
    public void Too_few_decided_bookings_yields_no_predictions()
    {
        var decided = LearnableSet(perOutcome: 5); // 10 rows, under the minimum
        var pending = new[] { new BookingApprovalModel.PendingBooking(Guid.NewGuid(), Row(30, false)) };

        var result = BookingApprovalModel.Run(decided, pending);

        Assert.False(result.HasSufficientData);
        Assert.Empty(result.ProbabilityByBookingId);
    }

    [Fact]
    public void A_fleet_that_has_only_ever_approved_yields_no_predictions()
    {
        // The realistic degenerate case: a new fleet that says yes to everything.
        // "Always approve" is not a pattern worth reporting as a prediction, and
        // single-class logistic regression would emit near-certainty on every row.
        var decided = Enumerable.Range(0, 50).Select(i => Row(10 + i, approved: true)).ToList();
        var pending = new[] { new BookingApprovalModel.PendingBooking(Guid.NewGuid(), Row(30, false)) };

        var result = BookingApprovalModel.Run(decided, pending);

        Assert.False(result.HasSufficientData);
        Assert.Empty(result.ProbabilityByBookingId);
    }

    [Fact]
    public void A_fleet_that_has_only_ever_rejected_yields_no_predictions()
    {
        var decided = Enumerable.Range(0, 50).Select(i => Row(10 + i, approved: false)).ToList();
        var pending = new[] { new BookingApprovalModel.PendingBooking(Guid.NewGuid(), Row(30, false)) };

        var result = BookingApprovalModel.Run(decided, pending);

        Assert.False(result.HasSufficientData);
    }

    [Fact]
    public void A_barely_one_sided_fleet_still_yields_no_predictions()
    {
        // Enough rows overall, but only two rejections — too few to characterise
        // what a rejection looks like.
        List<BookingApprovalModel.BookingFeatures> decided =
        [
            .. Enumerable.Range(0, 40).Select(i => Row(60 + i, approved: true)),
            .. Enumerable.Range(0, 2).Select(i => Row(1 + i, approved: false)),
        ];
        var pending = new[] { new BookingApprovalModel.PendingBooking(Guid.NewGuid(), Row(30, false)) };

        var result = BookingApprovalModel.Run(decided, pending);

        Assert.False(result.HasSufficientData);
    }

    [Fact]
    public void No_pending_bookings_is_sufficient_data_with_nothing_to_score()
    {
        var result = BookingApprovalModel.Run(LearnableSet(), []);

        Assert.True(result.HasSufficientData);
        Assert.Empty(result.ProbabilityByBookingId);
    }

    // ---------- Predictions ----------

    [Fact]
    public void Every_pending_booking_gets_a_probability_between_zero_and_one()
    {
        var pending = Enumerable.Range(0, 5)
            .Select(i => new BookingApprovalModel.PendingBooking(Guid.NewGuid(), Row(10 + i * 10, false)))
            .ToList();

        var result = BookingApprovalModel.Run(LearnableSet(), pending);

        Assert.True(result.HasSufficientData);
        Assert.Equal(pending.Count, result.ProbabilityByBookingId.Count);
        Assert.All(result.ProbabilityByBookingId.Values, p => Assert.InRange(p, 0d, 1d));
    }

    [Fact]
    public void The_model_learns_the_pattern_in_its_training_data()
    {
        // Trained on "long lead time approved, last-minute rejected", a 90-day
        // request should score above a 1-day one. If this fails the features are
        // not reaching the trainer, whatever else still passes.
        var comfortable = Guid.NewGuid();
        var lastMinute = Guid.NewGuid();

        var result = BookingApprovalModel.Run(
            LearnableSet(),
            [
                new BookingApprovalModel.PendingBooking(comfortable, Row(90, false)),
                new BookingApprovalModel.PendingBooking(lastMinute, Row(1, false)),
            ]);

        Assert.True(
            result.ProbabilityByBookingId[comfortable] > result.ProbabilityByBookingId[lastMinute],
            $"expected the 90-day-lead request ({result.ProbabilityByBookingId[comfortable]:P1}) to score above "
            + $"the next-day one ({result.ProbabilityByBookingId[lastMinute]:P1})");
    }

    [Fact]
    public void Training_is_deterministic_across_runs()
    {
        // A fixed MLContext seed means an admin refreshing the queue sees the same
        // number twice, rather than a score that drifts for no visible reason.
        var bookingId = Guid.NewGuid();
        var pending = new[] { new BookingApprovalModel.PendingBooking(bookingId, Row(45, false)) };

        var first = BookingApprovalModel.Run(LearnableSet(), pending);
        var second = BookingApprovalModel.Run(LearnableSet(), pending);

        Assert.Equal(first.ProbabilityByBookingId[bookingId], second.ProbabilityByBookingId[bookingId]);
    }

    [Fact]
    public void An_unseen_category_value_does_not_throw()
    {
        // A category present in the queue but absent from training history is
        // normal after a fleet adds its first bus; one-hot encoding must treat it
        // as unknown rather than fail the whole request.
        var bookingId = Guid.NewGuid();

        var result = BookingApprovalModel.Run(
            LearnableSet(),
            [new BookingApprovalModel.PendingBooking(bookingId, Row(30, false, eventType: "Wedding", carCategory: "Bus"))]);

        Assert.True(result.HasSufficientData);
        Assert.InRange(result.ProbabilityByBookingId[bookingId], 0d, 1d);
    }
}
