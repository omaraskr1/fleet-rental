using FleetRental.Domain.Common;
using FleetRental.Domain.Entities;
using FleetRental.Domain.Enums;
using FleetRental.Domain.ValueObjects;

namespace FleetRental.UnitTests.Domain;

public class BookingTests
{
    private static readonly Guid ClientId = Guid.CreateVersion7();
    private static readonly Guid EventId = Guid.CreateVersion7();
    private static readonly Guid AdminId = Guid.CreateVersion7();

    private static Car ActiveCar() =>
        Car.Create("Test Car", "desc", CarCategory.Sedan, 5, 200m);

    private static DateRange FutureRange(int startOffset = 10, int days = 4)
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(startOffset);
        return new DateRange(start, start.AddDays(days - 1));
    }

    private static Booking PendingBooking() =>
        Booking.Request(ActiveCar(), ClientId, EventId, FutureRange());

    // ---------- Creation ----------

    [Fact]
    public void New_booking_starts_pending_and_holds_no_days()
    {
        var booking = PendingBooking();

        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.True(booking.IsOpen);
        Assert.False(booking.BlocksDates);

        // Pending requests must not hold dates — several clients are allowed to
        // request the same range and compete for it.
        Assert.Empty(booking.BookedDays);
    }

    [Fact]
    public void Request_rejects_a_car_that_is_not_bookable()
    {
        var car = ActiveCar();
        car.ChangeStatus(CarStatus.Maintenance);

        var ex = Assert.Throws<DomainException>(
            () => Booking.Request(car, ClientId, EventId, FutureRange()));

        Assert.Contains("not currently accepting bookings", ex.Message);
    }

    [Fact]
    public void Request_rejects_a_start_date_in_the_past()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-1);
        var range = new DateRange(yesterday, yesterday.AddDays(3));

        var ex = Assert.Throws<DomainException>(
            () => Booking.Request(ActiveCar(), ClientId, EventId, range));

        Assert.Contains("cannot be in the past", ex.Message);
    }

    [Fact]
    public void Request_allows_a_booking_starting_today()
    {
        // Same-day hire is legitimate; the past-date guard must not exclude today.
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var booking = Booking.Request(ActiveCar(), ClientId, EventId, new DateRange(today, today));

        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    // ---------- Approval ----------

    [Fact]
    public void Approve_claims_one_BookedDay_per_calendar_day()
    {
        var range = FutureRange(days: 5);
        var booking = Booking.Request(ActiveCar(), ClientId, EventId, range);

        booking.Approve(AdminId, "Confirmed");

        Assert.Equal(BookingStatus.Approved, booking.Status);
        Assert.True(booking.BlocksDates);

        // One row per day is what the unique index acts on. Off-by-one here would
        // leave a day unprotected at one end of every booking.
        Assert.Equal(5, booking.BookedDays.Count);
        Assert.Equal(range.EnumerateDays().ToList(), booking.BookedDays.Select(d => d.Date).Order().ToList());
    }

    [Fact]
    public void Approve_records_who_decided_and_why()
    {
        var booking = PendingBooking();

        booking.Approve(AdminId, "  Confirmed by phone  ");

        Assert.Equal(AdminId, booking.DecidedByUserId);
        Assert.Equal("Confirmed by phone", booking.DecisionReason);
        Assert.NotNull(booking.DecidedAt);
    }

    [Fact]
    public void Approve_twice_is_rejected()
    {
        var booking = PendingBooking();
        booking.Approve(AdminId);

        var ex = Assert.Throws<DomainException>(() => booking.Approve(AdminId));
        Assert.Contains("Only pending bookings can be approved", ex.Message);
    }

    [Fact]
    public void Approving_a_rejected_booking_is_refused()
    {
        var booking = PendingBooking();
        booking.Reject(AdminId, "No");

        Assert.Throws<DomainException>(() => booking.Approve(AdminId));
    }

    // ---------- Rejection ----------

    [Fact]
    public void Reject_holds_no_dates()
    {
        var booking = PendingBooking();

        booking.Reject(AdminId, "Vehicle committed elsewhere");

        Assert.Equal(BookingStatus.Rejected, booking.Status);
        Assert.False(booking.BlocksDates);
        Assert.Empty(booking.BookedDays);
        Assert.Equal("Vehicle committed elsewhere", booking.DecisionReason);
    }

    [Fact]
    public void Reject_twice_is_refused()
    {
        var booking = PendingBooking();
        booking.Reject(AdminId);

        Assert.Throws<DomainException>(() => booking.Reject(AdminId));
    }

    // ---------- Cancellation ----------

    [Fact]
    public void Cancelling_an_approved_booking_releases_its_days()
    {
        var booking = PendingBooking();
        booking.Approve(AdminId);
        Assert.NotEmpty(booking.BookedDays);

        booking.Cancel();

        Assert.Equal(BookingStatus.Cancelled, booking.Status);

        // If the days were not released the car would stay blocked forever on
        // dates nobody is actually using.
        Assert.Empty(booking.BookedDays);
        Assert.False(booking.BlocksDates);
    }

    [Fact]
    public void Pending_booking_can_be_cancelled()
    {
        var booking = PendingBooking();
        booking.Cancel();
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }

    [Theory]
    [InlineData(BookingStatus.Rejected)]
    [InlineData(BookingStatus.Cancelled)]
    public void Cancelling_a_closed_booking_is_refused(BookingStatus status)
    {
        var booking = PendingBooking();

        if (status == BookingStatus.Rejected)
        {
            booking.Reject(AdminId);
        }
        else
        {
            booking.Cancel();
        }

        Assert.Throws<DomainException>(booking.Cancel);
    }

    // ---------- Rescheduling ----------

    [Fact]
    public void Reschedule_moves_a_pending_booking()
    {
        var booking = PendingBooking();
        var newRange = FutureRange(startOffset: 40, days: 2);

        booking.Reschedule(newRange);

        Assert.Equal(newRange, booking.Period);
        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Fact]
    public void Reschedule_is_refused_once_approved()
    {
        var booking = PendingBooking();
        booking.Approve(AdminId);

        // Allowing this would move the booking without moving its BookedDay rows,
        // silently freeing dates that are still held and holding dates that are not.
        Assert.Throws<DomainException>(() => booking.Reschedule(FutureRange(startOffset: 50)));
    }

    [Fact]
    public void Reschedule_rejects_past_dates()
    {
        var booking = PendingBooking();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-1);

        Assert.Throws<DomainException>(() => booking.Reschedule(new DateRange(yesterday, yesterday)));
    }

    // ---------- Notification bookkeeping ----------

    [Fact]
    public void MarkNotified_is_what_stops_a_client_being_told_twice()
    {
        var booking = PendingBooking();
        booking.Approve(AdminId);
        Assert.Null(booking.NotifiedAt);

        booking.MarkNotified();

        Assert.NotNull(booking.NotifiedAt);
    }
}
