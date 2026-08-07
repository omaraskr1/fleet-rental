using FleetRental.Domain.Common;
using FleetRental.Domain.ValueObjects;

namespace FleetRental.UnitTests.Domain;

/// <summary>
/// DateRange.OverlapsWith is the single definition of "double-booked" in the
/// system. The database constraint, the availability service, and the approval
/// path all have to agree with it, so it gets the most thorough table here.
/// </summary>
public class DateRangeTests
{
    private static DateOnly D(int day) => new(2026, 10, day);

    [Fact]
    public void Constructor_rejects_end_before_start()
    {
        var ex = Assert.Throws<DomainException>(() => new DateRange(D(5), D(1)));
        Assert.Contains("cannot fall before", ex.Message);
    }

    [Fact]
    public void Single_day_range_is_valid()
    {
        var range = new DateRange(D(3), D(3));
        Assert.Equal(1, range.TotalDays);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 2, 2)]
    [InlineData(1, 5, 5)]
    [InlineData(1, 31, 31)]
    public void TotalDays_counts_both_endpoints(int start, int end, int expected)
    {
        Assert.Equal(expected, new DateRange(D(start), D(end)).TotalDays);
    }

    // Reference range for every overlap case below is 10..14 inclusive.
    [Theory]
    // --- must overlap ---
    [InlineData(10, 14, true, "identical")]
    [InlineData(11, 13, true, "fully inside")]
    [InlineData(8, 20, true, "fully contains")]
    [InlineData(8, 10, true, "touches start day")]
    [InlineData(14, 20, true, "touches end day")]
    [InlineData(8, 12, true, "overlaps front half")]
    [InlineData(12, 20, true, "overlaps back half")]
    [InlineData(10, 10, true, "single day on start")]
    [InlineData(14, 14, true, "single day on end")]
    [InlineData(12, 12, true, "single day inside")]
    // --- must NOT overlap ---
    [InlineData(1, 9, false, "ends day before")]
    [InlineData(15, 20, false, "starts day after")]
    [InlineData(1, 5, false, "far before")]
    [InlineData(20, 25, false, "far after")]
    [InlineData(9, 9, false, "single day just before")]
    [InlineData(15, 15, false, "single day just after")]
    public void OverlapsWith_matches_expected(int start, int end, bool expected, string because)
    {
        var reference = new DateRange(D(10), D(14));
        var other = new DateRange(D(start), D(end));

        Assert.True(reference.OverlapsWith(other) == expected, $"forward: {because}");

        // Overlap is symmetric. If this ever diverges, whichever side the service
        // happens to call would decide the answer — a genuinely nasty bug.
        Assert.True(other.OverlapsWith(reference) == expected, $"reverse: {because}");
    }

    [Fact]
    public void Adjacent_ranges_do_not_overlap_so_back_to_back_rentals_are_allowed()
    {
        // A car returned on the 14th can go out again on the 15th. If this were
        // true, the fleet would lose a bookable day between every rental.
        var first = new DateRange(D(10), D(14));
        var second = new DateRange(D(15), D(18));

        Assert.False(first.OverlapsWith(second));
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(12, true)]
    [InlineData(14, true)]
    [InlineData(15, false)]
    public void Contains_is_inclusive_of_both_endpoints(int day, bool expected)
    {
        Assert.Equal(expected, new DateRange(D(10), D(14)).Contains(D(day)));
    }

    [Fact]
    public void EnumerateDays_yields_every_day_inclusive()
    {
        var days = new DateRange(D(10), D(13)).EnumerateDays().ToList();

        Assert.Equal(4, days.Count);
        Assert.Equal([D(10), D(11), D(12), D(13)], days);
    }

    [Fact]
    public void EnumerateDays_count_always_equals_TotalDays()
    {
        // These two are used interchangeably — TotalDays for display, EnumerateDays
        // to claim BookedDay rows. If they disagreed, the number of days shown to
        // the client would differ from the number actually held.
        var range = new DateRange(D(1), D(28));
        Assert.Equal(range.TotalDays, range.EnumerateDays().Count());
    }

    [Fact]
    public void EnumerateDays_spans_month_boundary()
    {
        var range = new DateRange(new DateOnly(2026, 10, 30), new DateOnly(2026, 11, 2));
        Assert.Equal([
            new DateOnly(2026, 10, 30),
            new DateOnly(2026, 10, 31),
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 2),
        ], range.EnumerateDays().ToList());
    }

    [Fact]
    public void EnumerateDays_handles_leap_day()
    {
        var range = new DateRange(new DateOnly(2028, 2, 28), new DateOnly(2028, 3, 1));
        Assert.Contains(new DateOnly(2028, 2, 29), range.EnumerateDays());
        Assert.Equal(3, range.TotalDays);
    }

    [Fact]
    public void Equal_ranges_are_equal_by_value()
    {
        Assert.Equal(new DateRange(D(1), D(5)), new DateRange(D(1), D(5)));
    }
}
