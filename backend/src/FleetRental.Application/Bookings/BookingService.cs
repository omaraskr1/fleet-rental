using FleetRental.Application.Abstractions;
using FleetRental.Application.Common;
using FleetRental.Application.Notifications;
using FleetRental.Domain.Entities;
using FleetRental.Domain.Enums;
using FleetRental.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace FleetRental.Application.Bookings;

public class BookingService(
    IFleetRentalDbContext db,
    BookingNotificationService notifications)
{
    /// <summary>
    /// Submits a booking request (feature 3), creating the event inline unless the
    /// client is adding a car to an activation they already registered.
    /// </summary>
    public async Task<BookingDto> CreateAsync(
        Guid clientId,
        CreateBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        var period = ParsePeriod(request.StartDate, request.EndDate);

        var car = await db.Cars
            .Include(c => c.Photos)
            .FirstOrDefaultAsync(c => c.Id == request.CarId, cancellationToken)
            ?? throw new NotFoundException(nameof(Car), request.CarId);

        var ev = request.ExistingEventId is { } eventId
            ? await LoadOwnedEventAsync(eventId, clientId, cancellationToken)
            : CreateEventFrom(clientId, request);

        if (request.ExistingEventId is null)
        {
            db.Events.Add(ev);
        }

        // Advisory pre-check: catches the overwhelmingly common case and produces a
        // clear message. It is not the guarantee — approval re-checks under a
        // transaction, and the unique index is the actual backstop.
        var alreadyTaken = await db.BookedDays
            .AnyAsync(
                d => d.CarId == car.Id && d.Date >= period.Start && d.Date <= period.End,
                cancellationToken);

        if (alreadyTaken)
        {
            throw new ConflictException(
                $"'{car.Name}' is already booked for part of {period}. Pick different dates.");
        }

        // Domain constructor enforces bookable-car and no-past-dates.
        var booking = Booking.Request(car, clientId, ev.Id, period, request.ClientNotes);
        db.Bookings.Add(booking);

        await db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(booking.Id, cancellationToken);
    }

    /// <summary>
    /// Approves a request (feature 4). The re-check and the day claims happen in one
    /// transaction so a concurrent approval cannot slip between them.
    /// </summary>
    public async Task<BookingDto> ApproveAsync(
        Guid bookingId,
        Guid adminId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var approved = await db.InTransactionAsync(async ct =>
        {
            var booking = await db.Bookings
                .Include(b => b.BookedDays)
                .FirstOrDefaultAsync(b => b.Id == bookingId, ct)
                ?? throw new NotFoundException(nameof(Booking), bookingId);

            if (booking.Status != BookingStatus.Pending)
            {
                throw new ConflictException(
                    $"This booking is already {booking.Status.ToString().ToLowerInvariant()}.");
            }

            // Re-read availability inside the transaction. Between the client
            // submitting and the admin clicking approve, another booking may have
            // taken these dates.
            var conflicts = await db.BookedDays
                .Where(d => d.CarId == booking.CarId
                    && d.Date >= booking.Period.Start
                    && d.Date <= booking.Period.End)
                .Select(d => d.Date)
                .ToListAsync(ct);

            if (conflicts.Count > 0)
            {
                throw new ConflictException(
                    $"Cannot approve: {conflicts.Count} day(s) in this range were booked by another request "
                    + $"({string.Join(", ", conflicts.Take(3).Select(d => d.ToString("yyyy-MM-dd")))}"
                    + $"{(conflicts.Count > 3 ? "…" : string.Empty)}).");
            }

            // Populates BookedDays for every day in the range.
            booking.Approve(adminId, reason);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (db.IsUniqueConstraintViolation(ex))
            {
                // Two admins approved conflicting requests simultaneously and both
                // passed the re-check above. The unique index caught it; this
                // transaction rolls back and the other approval stands.
                throw new ConflictException(
                    "Another approval claimed these dates a moment ago. Refresh and try again.");
            }

            return booking.Id;
        }, cancellationToken);

        var dto = await GetByIdAsync(approved, cancellationToken);

        // Sent after commit: a notification about an approval that then rolled back
        // would be worse than a late one.
        await notifications.NotifyDecisionAsync(approved, cancellationToken);

        return dto;
    }

    public async Task<BookingDto> RejectAsync(
        Guid bookingId,
        Guid adminId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var booking = await db.Bookings
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
            ?? throw new NotFoundException(nameof(Booking), bookingId);

        if (booking.Status != BookingStatus.Pending)
        {
            throw new ConflictException(
                $"This booking is already {booking.Status.ToString().ToLowerInvariant()}.");
        }

        // No transaction needed: rejection claims no dates, so nothing can race it.
        booking.Reject(adminId, reason);
        await db.SaveChangesAsync(cancellationToken);

        var dto = await GetByIdAsync(bookingId, cancellationToken);
        await notifications.NotifyDecisionAsync(bookingId, cancellationToken);

        return dto;
    }

    /// <summary>Client withdraws their own request, releasing any held days.</summary>
    public async Task<BookingDto> CancelAsync(
        Guid bookingId,
        Guid requestingUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var booking = await db.Bookings
            .Include(b => b.BookedDays)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
            ?? throw new NotFoundException(nameof(Booking), bookingId);

        if (!isAdmin && booking.ClientId != requestingUserId)
        {
            throw new ForbiddenException("You can only cancel your own bookings.");
        }

        // Clears BookedDays, freeing the dates for other clients.
        booking.Cancel();
        await db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(bookingId, cancellationToken);
    }

    public async Task<BookingDto> GetByIdAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await db.Bookings
            .AsNoTracking()
            .WithDetails()
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
            ?? throw new NotFoundException(nameof(Booking), bookingId);

        return BookingDto.FromEntity(booking);
    }

    /// <summary>The client's own requests, newest first.</summary>
    public async Task<IReadOnlyList<BookingDto>> GetForClientAsync(
        Guid clientId,
        BookingStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var bookings = await db.Bookings
            .AsNoTracking()
            .WithDetails()
            .Where(b => b.ClientId == clientId)
            .Where(b => status == null || b.Status == status)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        return [.. bookings.Select(BookingDto.FromEntity)];
    }

    /// <summary>The admin request queue — pending first, then most recent.</summary>
    public async Task<IReadOnlyList<BookingDto>> GetAllAsync(
        BookingStatus? status = null,
        Guid? carId = null,
        CancellationToken cancellationToken = default)
    {
        var bookings = await db.Bookings
            .AsNoTracking()
            .WithDetails()
            .Where(b => status == null || b.Status == status)
            .Where(b => carId == null || b.CarId == carId)
            .OrderBy(b => b.Status == BookingStatus.Pending ? 0 : 1)
            .ThenByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        return [.. bookings.Select(BookingDto.FromEntity)];
    }

    private async Task<Event> LoadOwnedEventAsync(Guid eventId, Guid clientId, CancellationToken cancellationToken)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken)
            ?? throw new NotFoundException(nameof(Event), eventId);

        // Without this, a client could attach their booking to someone else's event
        // and read its details back through the booking DTO.
        if (ev.OrganizerId != clientId)
        {
            throw new ForbiddenException("You can only add bookings to your own events.");
        }

        return ev;
    }

    private static Event CreateEventFrom(Guid clientId, CreateBookingRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.EventName))
        {
            errors[nameof(request.EventName)] = ["Event name is required when creating a new event."];
        }

        if (string.IsNullOrWhiteSpace(request.EventLocation))
        {
            errors[nameof(request.EventLocation)] = ["Event location is required when creating a new event."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return Event.Create(
            clientId,
            request.EventName!,
            request.EventType ?? Domain.Enums.EventType.Other,
            request.EventLocation!,
            request.ExpectedAttendance,
            request.EventNotes);
    }

    private static DateRange ParsePeriod(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw ValidationException.Single(nameof(CreateBookingRequest.EndDate),
                "End date cannot fall before the start date.");
        }

        return new DateRange(start, end);
    }
}
