using FleetRental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetRental.Infrastructure.Persistence.Configurations;

public class ServiceRecordConfiguration : IEntityTypeConfiguration<ServiceRecord>
{
    public void Configure(EntityTypeBuilder<ServiceRecord> builder)
    {
        builder.ToTable("ServiceRecords");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Description).IsRequired().HasMaxLength(500);
        builder.Property(s => s.PerformedBy).HasMaxLength(200);
        builder.Property(s => s.Cost).HasPrecision(18, 2);

        builder.HasOne(s => s.Car)
            .WithMany()
            .HasForeignKey(s => s.CarId)
            .OnDelete(DeleteBehavior.Restrict);

        // The service-history screen and the next-due calculation both read
        // "most recent records for this car" first.
        builder.HasIndex(s => new { s.CarId, s.PerformedAt });
    }
}
