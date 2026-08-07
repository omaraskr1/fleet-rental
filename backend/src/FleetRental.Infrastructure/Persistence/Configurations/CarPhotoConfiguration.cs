using FleetRental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetRental.Infrastructure.Persistence.Configurations;

public class CarPhotoConfiguration : IEntityTypeConfiguration<CarPhoto>
{
    public void Configure(EntityTypeBuilder<CarPhoto> builder)
    {
        builder.ToTable("CarPhotos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Url).IsRequired().HasMaxLength(1000);
        builder.Property(p => p.Caption).HasMaxLength(300);

        // Gallery ordering (Phase 2) reads straight off this.
        builder.HasIndex(p => new { p.CarId, p.SortOrder });
    }
}
