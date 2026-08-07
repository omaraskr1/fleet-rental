using System.Reflection;
using FleetRental.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetRental.Infrastructure.Persistence;

public class FleetRentalDbContext(DbContextOptions<FleetRentalDbContext> options) : DbContext(options)
{
    public DbSet<Car> Cars => Set<Car>();

    public DbSet<CarPhoto> CarPhotos => Set<CarPhoto>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<BookedDay> BookedDays => Set<BookedDay>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<User> Users => Set<User>();

    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
