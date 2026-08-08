using FleetRental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetRental.Infrastructure.Persistence.Configurations;

public class CarLocationConfiguration : IEntityTypeConfiguration<CarLocation>
{
    public void Configure(EntityTypeBuilder<CarLocation> builder)
    {
        builder.ToTable("CarLocations");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Latitude).HasPrecision(9, 6);
        builder.Property(l => l.Longitude).HasPrecision(9, 6);

        builder.HasOne(l => l.Car)
            .WithMany()
            .HasForeignKey(l => l.CarId)
            .OnDelete(DeleteBehavior.Restrict);

        // The map's "latest reading per car" query is the hot path.
        builder.HasIndex(l => new { l.CarId, l.RecordedAt });
    }
}
