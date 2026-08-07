namespace FleetRental.Application.Availability;

/// <summary>
/// Calendar payload for one car over a window (feature 2). The client renders
/// <see cref="BookedDates"/> as blocked cells; everything else in the window is open.
/// </summary>
/// <remarks>
/// Sending the booked days rather than every day in the window keeps the response
/// small — a mostly-free car in a 90-day window sends a handful of dates instead
/// of ninety entries that are almost all identical.
/// </remarks>
public sealed record CarAvailabilityDto
{
    public required Guid CarId { get; init; }

    public required string CarName { get; init; }

    public required DateOnly WindowStart { get; init; }

    public required DateOnly WindowEnd { get; init; }

    /// <summary>Days held by an approved booking. Everything else is bookable.</summary>
    public required IReadOnlyList<DateOnly> BookedDates { get; init; }

    /// <summary>
    /// Days with a pending request but no approval yet. Shown in a distinct colour
    /// so a client knows they may be competing for those dates.
    /// </summary>
    public required IReadOnlyList<DateOnly> PendingDates { get; init; }

    /// <summary>False when the car is in maintenance or retired — the whole window is unavailable.</summary>
    public required bool CarIsBookable { get; init; }
}

/// <summary>
/// Fleet-wide calendar for the admin panel (feature 4): one lane per car so the
/// owner can see the whole fleet's commitments in a single view.
/// </summary>
public sealed record FleetAvailabilityDto
{
    public required DateOnly WindowStart { get; init; }

    public required DateOnly WindowEnd { get; init; }

    public required IReadOnlyList<CarAvailabilityDto> Cars { get; init; }
}

/// <summary>Answer to "can I book this car for these dates?" before submitting.</summary>
public sealed record AvailabilityCheckDto
{
    public required bool IsAvailable { get; init; }

    /// <summary>Which requested days are already taken. Empty when available.</summary>
    public required IReadOnlyList<DateOnly> ConflictingDates { get; init; }

    public string? Reason { get; init; }
}
