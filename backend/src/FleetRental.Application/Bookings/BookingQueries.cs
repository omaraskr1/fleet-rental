using FleetRental.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetRental.Application.Bookings;

internal static class BookingQueries
{
    /// <summary>
    /// Loads the navigations <see cref="BookingDto.FromEntity"/> reads. Centralised
    /// so a new caller cannot forget one and hit a null reference at runtime.
    /// </summary>
    public static IQueryable<Booking> WithDetails(this IQueryable<Booking> query) =>
        query
            .Include(b => b.Car).ThenInclude(c => c.Photos)
            .Include(b => b.Client)
            .Include(b => b.Event);
}
