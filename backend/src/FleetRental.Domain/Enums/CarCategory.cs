namespace FleetRental.Domain.Enums;

/// <summary>
/// Marketing/event vehicle classes. Persisted as string so adding a category in
/// Phase 2 does not renumber existing rows.
/// </summary>
public enum CarCategory
{
    Sedan = 0,
    Suv = 1,
    Van = 2,
    Luxury = 3,
    Convertible = 4,
    BrandedTruck = 5,
    Bus = 6,
}
