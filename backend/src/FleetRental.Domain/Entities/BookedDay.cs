using FleetRental.Domain.Common;

namespace FleetRental.Domain.Entities;

/// <summary>
/// One row per car per calendar day held by an approved booking.
/// </summary>
/// <remarks>
/// This exists to make double-booking impossible at the database level. Range
/// overlap cannot be expressed as a unique constraint in SQL Server (there is no
/// equivalent of Postgres' EXCLUDE ... WITH &amp;&amp;), so an approved booking is
/// expanded into its individual days and a unique index on (CarId, Date) does the
/// enforcing. Two admins approving conflicting requests at the same instant means
/// the second INSERT violates the index and its transaction rolls back — a real
/// guarantee rather than one that depends on getting isolation levels right.
///
/// It pays for itself twice: the per-car calendar (feature 2) and the fleet-wide
/// calendar (feature 4) both read these rows directly instead of expanding ranges
/// on every request.
///
/// Rows are written when a booking is approved and removed when it is cancelled,
/// which is why <see cref="Booking"/> is the only thing that may create them.
/// </remarks>
public class BookedDay : Entity
{
    private BookedDay() { } // EF Core

    internal BookedDay(Guid carId, Guid bookingId, DateOnly date)
    {
        CarId = carId;
        BookingId = bookingId;
        Date = date;
    }

    public Guid CarId { get; private set; }

    public Car Car { get; private set; } = null!;

    public Guid BookingId { get; private set; }

    public Booking Booking { get; private set; } = null!;

    public DateOnly Date { get; private set; }
}
