using FleetRental.Domain.Common;
using FleetRental.Domain.Enums;
using FleetRental.Domain.ValueObjects;

namespace FleetRental.Domain.Entities;

/// <summary>
/// A rentable vehicle in the fleet. Aggregate root for its photos and the
/// authority on whether a given date range can be booked.
/// </summary>
public class Car : TenantEntity
{
    private readonly List<CarPhoto> _photos = [];
    private readonly List<Booking> _bookings = [];

    private Car() { } // EF Core

    private Car(
        string name,
        string description,
        CarCategory category,
        int seats,
        decimal rate,
        PricingModel pricingModel,
        string? licensePlate)
    {
        Name = name;
        Description = description;
        Category = category;
        Seats = seats;
        Rate = rate;
        PricingModel = pricingModel;
        LicensePlate = licensePlate;
    }

    public string Name { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    public CarCategory Category { get; private set; }

    public int Seats { get; private set; }

    /// <summary>
    /// Indicative price, read per <see cref="PricingModel"/>. Phase 1 takes no
    /// payment, so this is shown for information only and is not used to compute
    /// a booking total anywhere — Analytics is the one place that treats it as a
    /// number, for estimated revenue.
    /// </summary>
    public decimal Rate { get; private set; }

    /// <summary>Whether <see cref="Rate"/> applies per day or once per booking.</summary>
    public PricingModel PricingModel { get; private set; } = PricingModel.PerDay;

    public string? LicensePlate { get; private set; }

    public CarStatus Status { get; private set; } = CarStatus.Active;

    /// <summary>
    /// Most recent known reading. Nullable because odometer tracking is opt-in —
    /// entered by an admin, not read from telematics hardware this system does
    /// not have — and a car may simply never have had one recorded.
    /// </summary>
    public int? CurrentOdometerKm { get; private set; }

    /// <summary>
    /// Distance between services, if the owner tracks one for this car. Null
    /// means "not tracked", not "zero" — a car with no interval set is never
    /// flagged as due, rather than being flagged as permanently overdue.
    /// </summary>
    public int? ServiceIntervalKm { get; private set; }

    public IReadOnlyCollection<CarPhoto> Photos => _photos.AsReadOnly();

    public IReadOnlyCollection<Booking> Bookings => _bookings.AsReadOnly();

    /// <summary>Photo shown on the listing screen; falls back to the first if none is flagged.</summary>
    public CarPhoto? PrimaryPhoto => _photos.FirstOrDefault(p => p.IsPrimary) ?? _photos.MinBy(p => p.SortOrder);

    /// <summary>True when the car is listed and accepting requests.</summary>
    public bool IsBookable => Status == CarStatus.Active;

    public static Car Create(
        string name,
        string description,
        CarCategory category,
        int seats,
        decimal rate,
        string? licensePlate = null,
        PricingModel pricingModel = PricingModel.PerDay)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Car name is required.");
        }

        if (seats <= 0)
        {
            throw new DomainException("Seat count must be greater than zero.");
        }

        if (rate < 0)
        {
            throw new DomainException("Rate cannot be negative.");
        }

        return new Car(
            name.Trim(),
            description?.Trim() ?? string.Empty,
            category,
            seats,
            rate,
            pricingModel,
            licensePlate?.Trim());
    }

    public void UpdateDetails(
        string name,
        string description,
        CarCategory category,
        int seats,
        decimal rate,
        string? licensePlate,
        PricingModel pricingModel = PricingModel.PerDay)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Car name is required.");
        }

        if (seats <= 0)
        {
            throw new DomainException("Seat count must be greater than zero.");
        }

        if (rate < 0)
        {
            throw new DomainException("Rate cannot be negative.");
        }

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Category = category;
        Seats = seats;
        Rate = rate;
        PricingModel = pricingModel;
        LicensePlate = licensePlate?.Trim();
        Touch();
    }

    public void ChangeStatus(CarStatus status)
    {
        Status = status;
        Touch();
    }

    /// <summary>
    /// Records a new odometer reading — typically entered alongside a service,
    /// but also usable on its own so "due" can be judged between services.
    /// </summary>
    public void UpdateOdometer(int km)
    {
        if (km < 0)
        {
            throw new DomainException("Odometer reading cannot be negative.");
        }

        // A reading lower than one already on file is almost always a typo (or
        // an odometer swap after an engine replacement, rare enough that an
        // admin correcting it deliberately can go through support rather than
        // this silently accepting a rollback that breaks the due-date math).
        if (CurrentOdometerKm is { } current && km < current)
        {
            throw new DomainException(
                $"New odometer reading ({km} km) is lower than the current one on file ({current} km).");
        }

        CurrentOdometerKm = km;
        Touch();
    }

    public void SetServiceInterval(int? km)
    {
        if (km is < 1)
        {
            throw new DomainException("Service interval must be at least 1 km.");
        }

        ServiceIntervalKm = km;
        Touch();
    }

    /// <summary>
    /// Adds a photo. The first photo added becomes primary automatically so a car
    /// always has something to render on the listing screen.
    /// </summary>
    public CarPhoto AddPhoto(string url, string? caption = null, bool isPrimary = false)
    {
        var makePrimary = isPrimary || _photos.Count == 0;

        if (makePrimary)
        {
            foreach (var existing in _photos)
            {
                existing.SetPrimary(false);
            }
        }

        var photo = new CarPhoto(Id, url, caption, makePrimary, _photos.Count);
        _photos.Add(photo);
        Touch();
        return photo;
    }

    public void RemovePhoto(Guid photoId)
    {
        var photo = _photos.FirstOrDefault(p => p.Id == photoId);
        if (photo is null)
        {
            return;
        }

        _photos.Remove(photo);

        // Removing the primary would leave the listing screen with no image, so
        // promote the next one rather than allowing a car with photos but no primary.
        if (photo.IsPrimary && _photos.Count > 0)
        {
            _photos.MinBy(p => p.SortOrder)!.SetPrimary(true);
        }

        Touch();
    }

    /// <summary>
    /// The in-memory half of double-booking prevention: true when no approved
    /// booking touches <paramref name="range"/>.
    /// </summary>
    /// <remarks>
    /// This is necessary but NOT sufficient. Two requests arriving at the same
    /// instant can both pass this check before either commits, so the database
    /// also carries a constraint. See BookingService for the transactional path.
    /// </remarks>
    public bool IsAvailableFor(DateRange range, Guid? ignoreBookingId = null) =>
        !_bookings.Any(b =>
            b.Id != ignoreBookingId &&
            b.Status == BookingStatus.Approved &&
            b.Period.OverlapsWith(range));
}
