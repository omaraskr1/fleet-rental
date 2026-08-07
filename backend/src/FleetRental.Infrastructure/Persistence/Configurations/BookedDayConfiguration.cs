using FleetRental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetRental.Infrastructure.Persistence.Configurations;

public class BookedDayConfiguration : IEntityTypeConfiguration<BookedDay>
{
    public void Configure(EntityTypeBuilder<BookedDay> builder)
    {
        builder.ToTable("BookedDays");
        builder.HasKey(d => d.Id);

        builder.HasOne(d => d.Car)
            .WithMany()
            .HasForeignKey(d => d.CarId)
            .OnDelete(DeleteBehavior.Restrict);

        // THE double-booking guarantee. One car can hold a given calendar day
        // exactly once; a concurrent approval for an overlapping range fails here
        // with a unique-constraint violation and rolls back. Everything in the
        // service layer above this is a nicety for producing good error messages —
        // this is the line that actually cannot be crossed.
        builder.HasIndex(d => new { d.CarId, d.Date })
            .IsUnique()
            .HasDatabaseName("UX_BookedDays_Car_Date");

        // Calendar reads: "all held days for this fleet between X and Y".
        builder.HasIndex(d => d.Date);
    }
}
