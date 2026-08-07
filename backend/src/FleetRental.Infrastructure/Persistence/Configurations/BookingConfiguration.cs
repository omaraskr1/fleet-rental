using FleetRental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetRental.Infrastructure.Persistence.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(b => b.ClientNotes).HasMaxLength(2000);
        builder.Property(b => b.DecisionReason).HasMaxLength(1000);

        // DateRange is a value object with no identity of its own, so it maps to two
        // columns on this table rather than a table of its own.
        builder.ComplexProperty(b => b.Period, period =>
        {
            period.Property(p => p.Start).HasColumnName("StartDate").IsRequired();
            period.Property(p => p.End).HasColumnName("EndDate").IsRequired();
        });

        builder.HasOne(b => b.Event)
            .WithMany(e => e.Bookings)
            .HasForeignKey(b => b.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(b => b.BookedDays)
            .WithOne(d => d.Booking)
            .HasForeignKey(d => d.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(b => b.BookedDays).UsePropertyAccessMode(PropertyAccessMode.Field);

        // The admin request queue is "pending, newest first".
        builder.HasIndex(b => b.Status);

        // Per-car calendar and availability lookups.
        builder.HasIndex(b => new { b.CarId, b.Status });

        // "My bookings" on the client app.
        builder.HasIndex(b => b.ClientId);

        builder.Ignore(b => b.BlocksDates);
        builder.Ignore(b => b.IsOpen);
    }
}
