using FleetRental.Domain.Common;
using FleetRental.Domain.Enums;
using FleetRental.Domain.ValueObjects;

namespace FleetRental.Domain.Entities;

/// <summary>
/// A rentable vehicle in the fleet. Aggregate root for its photos and the
/// authority on whether a given date range can be booked.
/// </summary>
public class Car : Entity
{
    private readonly List<CarPhoto> _photos = [];
    private readonly List<Booking> _bookings = [];

    private Car() { } // EF Core

    private Car(string name, string description, CarCategory category, int seats, decimal dailyRate, string? licensePlate)
    {
        Name = name;
        Description = description;
        Category = category;
        Seats = seats;
        DailyRate = dailyRate;
        LicensePlate = licensePlate;
    }

    public string Name { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    public CarCategory Category { get; private set; }

    public int Seats { get; private set; }

    /// <summary>
    /// Indicative daily price. Phase 1 takes no payment, so this is shown for
    /// information only and is not used to compute a total anywhere.
    /// </summary>
    public decimal DailyRate { get; private set; }

    public string? LicensePlate { get; private set; }

    public CarStatus Status { get; private set; } = CarStatus.Active;

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
        decimal dailyRate,
        string? licensePlate = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Car name is required.");
        }

        if (seats <= 0)
        {
            throw new DomainException("Seat count must be greater than zero.");
        }

        if (dailyRate < 0)
        {
            throw new DomainException("Daily rate cannot be negative.");
        }

        return new Car(
            name.Trim(),
            description?.Trim() ?? string.Empty,
            category,
            seats,
            dailyRate,
            licensePlate?.Trim());
    }

    public void UpdateDetails(
        string name,
        string description,
        CarCategory category,
        int seats,
        decimal dailyRate,
        string? licensePlate)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Car name is required.");
        }

        if (seats <= 0)
        {
            throw new DomainException("Seat count must be greater than zero.");
        }

        if (dailyRate < 0)
        {
            throw new DomainException("Daily rate cannot be negative.");
        }

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Category = category;
        Seats = seats;
        DailyRate = dailyRate;
        LicensePlate = licensePlate?.Trim();
        Touch();
    }

    public void ChangeStatus(CarStatus status)
    {
        Status = status;
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
