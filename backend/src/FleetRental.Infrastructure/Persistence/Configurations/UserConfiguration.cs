using FleetRental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetRental.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(512);
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(200);
        builder.Property(u => u.PhoneNumber).HasMaxLength(32);

        // Stored as text: adding a role in Phase 3 must not depend on enum ordering.
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(32).IsRequired();

        // One account per email address, enforced by the database rather than by a
        // check-then-insert in the signup handler.
        builder.HasIndex(u => u.Email).IsUnique();

        builder.HasMany(u => u.Bookings)
            .WithOne(b => b.Client)
            .HasForeignKey(b => b.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.DeviceTokens)
            .WithOne(t => t.User)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(u => u.Bookings).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(u => u.DeviceTokens).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
