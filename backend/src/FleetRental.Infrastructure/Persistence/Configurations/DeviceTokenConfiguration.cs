using FleetRental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetRental.Infrastructure.Persistence.Configurations;

public class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("DeviceTokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Token).IsRequired().HasMaxLength(512);
        builder.Property(t => t.DeviceId).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Platform).HasConversion<string>().HasMaxLength(16).IsRequired();

        // One row per device per user, so a token refresh updates rather than
        // piling up stale entries that would each get a duplicate push.
        builder.HasIndex(t => new { t.UserId, t.DeviceId }).IsUnique();
    }
}
