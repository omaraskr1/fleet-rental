using FleetRental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetRental.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Code).IsRequired().HasMaxLength(64);
        builder.Property(t => t.ContactEmail).HasMaxLength(256);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        // The code is what a client types on first launch, so it must resolve to
        // exactly one business platform-wide.
        builder.HasIndex(t => t.Code).IsUnique().HasDatabaseName("UX_Tenants_Code");

        builder.Ignore(t => t.IsActive);
    }
}
