using FleetRental.Domain.Common;

namespace FleetRental.Domain.Entities;

/// <summary>
/// One image for a car. Phase 1 renders only the primary photo on the listing
/// screen; Phase 2's gallery reads the same rows, so no migration is needed then.
/// </summary>
public class CarPhoto : TenantEntity
{
    private CarPhoto() { } // EF Core

    internal CarPhoto(Guid carId, string url, string? caption, bool isPrimary, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new DomainException("Photo url is required.");
        }

        CarId = carId;
        Url = url.Trim();
        Caption = caption?.Trim();
        IsPrimary = isPrimary;
        SortOrder = sortOrder;
    }

    public Guid CarId { get; private set; }

    public Car Car { get; private set; } = null!;

    public string Url { get; private set; } = null!;

    public string? Caption { get; private set; }

    /// <summary>Exactly one photo per car carries this flag; <see cref="Car"/> enforces it.</summary>
    public bool IsPrimary { get; private set; }

    public int SortOrder { get; private set; }

    internal void SetPrimary(bool isPrimary)
    {
        IsPrimary = isPrimary;
        Touch();
    }

    internal void SetSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
        Touch();
    }
}
