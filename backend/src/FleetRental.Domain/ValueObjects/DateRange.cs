using FleetRental.Domain.Common;

namespace FleetRental.Domain.ValueObjects;

/// <summary>
/// An inclusive range of whole rental days. A single-day rental has
/// <see cref="Start"/> == <see cref="End"/>.
/// </summary>
/// <remarks>
/// Rentals are billed and scheduled by the day, so <see cref="DateOnly"/> is used
/// deliberately instead of a timestamp. This sidesteps an entire class of timezone
/// bugs: a client in Dubai and an admin in London must agree on what "the 5th" means,
/// and they only do if no clock time is involved.
/// </remarks>
public readonly record struct DateRange
{
    public DateOnly Start { get; }

    public DateOnly End { get; }

    public DateRange(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new DomainException(
                $"Booking end date ({end:yyyy-MM-dd}) cannot fall before the start date ({start:yyyy-MM-dd}).");
        }

        Start = start;
        End = end;
    }

    /// <summary>Number of chargeable days, counting both endpoints.</summary>
    public int TotalDays => End.DayNumber - Start.DayNumber + 1;

    /// <summary>
    /// True when the two ranges share at least one day. This is the single
    /// definition of "double-booked" in the system — the database constraint and
    /// the service-layer check must both agree with it.
    /// </summary>
    public bool OverlapsWith(DateRange other) => Start <= other.End && other.Start <= End;

    public bool Contains(DateOnly date) => date >= Start && date <= End;

    /// <summary>Every day in the range, for painting calendar cells.</summary>
    public IEnumerable<DateOnly> EnumerateDays()
    {
        for (var day = Start; day <= End; day = day.AddDays(1))
        {
            yield return day;
        }
    }

    public override string ToString() => $"{Start:yyyy-MM-dd}..{End:yyyy-MM-dd}";
}
