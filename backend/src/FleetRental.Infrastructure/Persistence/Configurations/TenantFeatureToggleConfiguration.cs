using FleetRental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetRental.Infrastructure.Persistence.Configurations;

public class TenantFeatureToggleConfiguration : IEntityTypeConfiguration<TenantFeatureToggle>
{
    public void Configure(EntityTypeBuilder<TenantFeatureToggle> builder)
    {
        builder.ToTable("TenantFeatureToggles");
        builder.HasKey(t => t.Id);

        // Stored as text so a reordered enum never silently changes meaning.
        builder.Property(t => t.FeatureKey).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.HasIndex(t => new { t.TenantId, t.FeatureKey })
            .IsUnique()
            .HasDatabaseName("UX_TenantFeatureToggles_Tenant_FeatureKey");
    }
}
