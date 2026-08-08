using FleetRental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetRental.Infrastructure.Persistence.Configurations;

public class ServiceTypeConfiguration : IEntityTypeConfiguration<ServiceType>
{
    public void Configure(EntityTypeBuilder<ServiceType> builder)
    {
        builder.ToTable("ServiceTypes");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(150);

        // The catalog picker and the log-service dropdown both filter on active
        // status first.
        builder.HasIndex(t => new { t.TenantId, t.IsActive });
    }
}
