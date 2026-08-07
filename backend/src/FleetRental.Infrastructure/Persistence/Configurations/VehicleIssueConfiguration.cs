using FleetRental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetRental.Infrastructure.Persistence.Configurations;

public class VehicleIssueConfiguration : IEntityTypeConfiguration<VehicleIssue>
{
    public void Configure(EntityTypeBuilder<VehicleIssue> builder)
    {
        builder.ToTable("VehicleIssues");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Description).IsRequired().HasMaxLength(1000);
        builder.Property(i => i.ResolutionNotes).HasMaxLength(1000);
        builder.Property(i => i.Severity).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.HasOne(i => i.Car)
            .WithMany()
            .HasForeignKey(i => i.CarId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Entities.User>()
            .WithMany()
            .HasForeignKey(i => i.ReportedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // The owner's issue dashboard reads "open issues for this car" and
        // "every open issue across the fleet" — both filter on Status first.
        builder.HasIndex(i => new { i.CarId, i.Status });
        builder.HasIndex(i => i.Status);

        builder.Ignore(i => i.BlocksBooking);
    }
}
