using FleetRental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetRental.Infrastructure.Persistence.Configurations;

public class PlatformAdminConfiguration : IEntityTypeConfiguration<PlatformAdmin>
{
    public void Configure(EntityTypeBuilder<PlatformAdmin> builder)
    {
        builder.ToTable("PlatformAdmins");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Email).IsRequired().HasMaxLength(256);
        builder.Property(a => a.PasswordHash).IsRequired().HasMaxLength(512);
        builder.Property(a => a.FullName).IsRequired().HasMaxLength(200);

        // Unlike Users (unique per tenant), a platform admin is unique platform-wide
        // — there is no tenant to scope the uniqueness to.
        builder.HasIndex(a => a.Email).IsUnique().HasDatabaseName("UX_PlatformAdmins_Email");
    }
}
