using FleetRental.Application.Abstractions;
using FleetRental.Application.Common;
using FleetRental.Domain.Enums;
using FleetRental.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace FleetRental.Application.Availability;

/// <summary>
/// Read-side for both calendars. Every query here reads BookedDays rather than
/// expanding booking ranges in memory, which is why the fleet-wide view stays one
/// indexed query no matter how many cars are in the fleet.
/// </summary>
public class AvailabilityService(IFleetRentalDbContext db)
{
    /// <summary>How far ahead the calendar looks when the caller does not say.</summary>
    private const int DefaultWindowDays = 90;

    /// <summary>Per-car calendar (feature 2).</summary>
    public async Task<CarAvailabilityDto> GetCarAvailabilityAsync(
        Guid carId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = ResolveWindow(from, to);

        var car = await db.Cars
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == carId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Car), carId);

        var bookedDates = await db.BookedDays
            .AsNoTracking()
            .Where(d => d.CarId == carId && d.Date >= start && d.Date <= end)
            .Select(d => d.Date)
            .OrderBy(d => d)
            .ToListAsync(cancellationToken);

        var pendingDates = await GetPendingDatesAsync([carId], start, end, cancellationToken);

        return new CarAvailabilityDto
        {
            CarId = car.Id,
            CarName = car.Name,
            WindowStart = start,
            WindowEnd = end,
            BookedDates = bookedDates,
            PendingDates = pendingDates.GetValueOrDefault(carId, []),
            CarIsBookable = car.IsBookable,
        };
    }

    /// <summary>Fleet-wide calendar for the admin panel (feature 4).</summary>
    public async Task<FleetAvailabilityDto> GetFleetAvailabilityAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        bool includeRetired = false,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = ResolveWindow(from, to);

        var cars = await db.Cars
            .AsNoTracking()
            .Where(c => includeRetired || c.Status != CarStatus.Retired)
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.Status })
            .ToListAsync(cancellationToken);

        var carIds = cars.Select(c => c.Id).ToList();

        // One query for the whole fleet's booked days, then grouped in memory.
        // Beats a per-car round trip, which is what makes the admin calendar cheap.
        var bookedByCar = (await db.BookedDays
                .AsNoTracking()
                .Where(d => carIds.Contains(d.CarId) && d.Date >= start && d.Date <= end)
                .Select(d => new { d.CarId, d.Date })
                .ToListAsync(cancellationToken))
            .GroupBy(d => d.CarId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DateOnly>)[.. g.Select(x => x.Date).OrderBy(d => d)]);

        var pendingByCar = await GetPendingDatesAsync(carIds, start, end, cancellationToken);

        return new FleetAvailabilityDto
        {
            WindowStart = start,
            WindowEnd = end,
            Cars =
            [
                .. cars.Select(c => new CarAvailabilityDto
                {
                    CarId = c.Id,
                    CarName = c.Name,
                    WindowStart = start,
                    WindowEnd = end,
                    BookedDates = bookedByCar.GetValueOrDefault(c.Id, []),
                    PendingDates = pendingByCar.GetValueOrDefault(c.Id, []),
                    CarIsBookable = c.Status == CarStatus.Active,
                }),
            ],
        };
    }

    /// <summary>
    /// Pre-submit check so the booking form can warn before the client fills in
    /// event details. Advisory only — approval re-checks inside a transaction.
    /// </summary>
    public async Task<AvailabilityCheckDto> CheckAsync(
        Guid carId,
        DateRange period,
        CancellationToken cancellationToken = default)
    {
        var car = await db.Cars
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == carId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Car), carId);

        if (!car.IsBookable)
        {
            return new AvailabilityCheckDto
            {
                IsAvailable = false,
                ConflictingDates = [],
                Reason = $"'{car.Name}' is not currently accepting bookings.",
            };
        }

        var conflicts = await db.BookedDays
            .AsNoTracking()
            .Where(d => d.CarId == carId && d.Date >= period.Start && d.Date <= period.End)
            .Select(d => d.Date)
            .OrderBy(d => d)
            .ToListAsync(cancellationToken);

        return new AvailabilityCheckDto
        {
            IsAvailable = conflicts.Count == 0,
            ConflictingDates = conflicts,
            Reason = conflicts.Count == 0
                ? null
                : $"{conflicts.Count} of the requested {period.TotalDays} day(s) are already booked.",
        };
    }

    /// <summary>
    /// Days covered by pending requests. These do not block anything — they are
    /// shown so a client can see they may be competing for the dates.
    /// </summary>
    private async Task<Dictionary<Guid, IReadOnlyList<DateOnly>>> GetPendingDatesAsync(
        IReadOnlyCollection<Guid> carIds,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        // Pending bookings have no BookedDay rows, so their ranges are fetched and
        // expanded here. The set is small: only undecided requests in the window.
        var pending = await db.Bookings
            .AsNoTracking()
            .Where(b => carIds.Contains(b.CarId) && b.Status == BookingStatus.Pending)
            .Select(b => new { b.CarId, b.Period.Start, b.Period.End })
            .ToListAsync(cancellationToken);

        return pending
            .GroupBy(b => b.CarId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<DateOnly>)
                [
                    .. g.SelectMany(b => new DateRange(b.Start, b.End).EnumerateDays())
                        .Where(d => d >= start && d <= end)
                        .Distinct()
                        .OrderBy(d => d),
                ]);
    }

    private static (DateOnly Start, DateOnly End) ResolveWindow(DateOnly? from, DateOnly? to)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var end = to ?? start.AddDays(DefaultWindowDays);

        if (end < start)
        {
            throw ValidationException.Single(nameof(to), "Window end cannot fall before window start.");
        }

        return (start, end);
    }
}
