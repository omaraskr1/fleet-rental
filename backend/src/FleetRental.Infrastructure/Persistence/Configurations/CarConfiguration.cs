using FleetRental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetRental.Infrastructure.Persistence.Configurations;

public class CarConfiguration : IEntityTypeConfiguration<Car>
{
    public void Configure(EntityTypeBuilder<Car> builder)
    {
        builder.ToTable("Cars");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(150);
        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.Property(c => c.LicensePlate).HasMaxLength(32);
        builder.Property(c => c.GpsDeviceKey).HasMaxLength(64);

        // The ingestion endpoint resolves a car by this key with no tenant
        // context to narrow the search, so it must be unique platform-wide.
        builder.HasIndex(c => c.GpsDeviceKey).IsUnique().HasFilter("[GpsDeviceKey] IS NOT NULL");

        builder.Property(c => c.Category).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(c => c.PricingModel).HasConversion<string>().HasMaxLength(16).IsRequired();

        // Money: exact decimal, never float.
        builder.Property(c => c.Rate).HasPrecision(18, 2);

        // The listing screen filters on status, so index it.
        builder.HasIndex(c => c.Status);

        builder.HasMany(c => c.Photos)
            .WithOne(p => p.Car)
            .HasForeignKey(p => p.CarId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Bookings)
            .WithOne(b => b.Car)
            .HasForeignKey(b => b.CarId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(c => c.Photos).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(c => c.Bookings).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(c => c.PrimaryPhoto);
        builder.Ignore(c => c.IsBookable);
    }
}
