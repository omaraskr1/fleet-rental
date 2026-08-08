using FleetRental.Domain.Common;

namespace FleetRental.Domain.Entities;

/// <summary>
/// A named, recurring service the fleet owner tracks per car — "Oil change every
/// 10,000 km", "Brake check every 20,000 km". Defined once per tenant and shared
/// across every car, then referenced by <see cref="ServiceRecord"/> entries as
/// they're logged, so due-status can be computed independently per service type
/// rather than one generic interval for the whole car.
/// </summary>
public class ServiceType : TenantEntity
{
    private ServiceType() { } // EF Core

    private ServiceType(string name, int intervalKm)
    {
        Name = name;
        IntervalKm = intervalKm;
        IsActive = true;
    }

    public string Name { get; private set; } = null!;

    public int IntervalKm { get; private set; }

    /// <summary>
    /// Deactivated types stay in the catalog (any past ServiceRecord still points
    /// at one) but drop out of the "log a service" picker and the due-tracking
    /// list — never hard-deleted, same reasoning as Car.Retire.
    /// </summary>
    public bool IsActive { get; private set; }

    public static ServiceType Create(string name, int intervalKm)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Service name is required.");
        }

        if (intervalKm < 1)
        {
            throw new DomainException("Service interval must be at least 1 km.");
        }

        return new ServiceType(name.Trim(), intervalKm);
    }

    public void UpdateDetails(string name, int intervalKm)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Service name is required.");
        }

        if (intervalKm < 1)
        {
            throw new DomainException("Service interval must be at least 1 km.");
        }

        Name = name.Trim();
        IntervalKm = intervalKm;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    public void Reactivate()
    {
        IsActive = true;
        Touch();
    }
}
