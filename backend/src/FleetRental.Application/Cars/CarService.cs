using FleetRental.Application.Abstractions;
using FleetRental.Application.Common;
using FleetRental.Domain.Entities;
using FleetRental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FleetRental.Application.Cars;

public class CarService(IFleetRentalDbContext db)
{
    /// <summary>
    /// The browse screen (feature 1). Retired cars are hidden from clients but
    /// visible to admins, who need them for historical bookings.
    /// </summary>
    public async Task<IReadOnlyList<CarListItemDto>> GetListAsync(
        bool includeUnavailable = false,
        CarCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        var cars = await db.Cars
            .AsNoTracking()
            .Include(c => c.Photos)
            .Where(c => includeUnavailable || c.Status == CarStatus.Active)
            .Where(c => category == null || c.Category == category)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        // One query for today's holds across the whole list, rather than one per card.
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var busyToday = await db.BookedDays
            .AsNoTracking()
            .Where(d => d.Date == today)
            .Select(d => d.CarId)
            .ToListAsync(cancellationToken);

        var busy = busyToday.ToHashSet();

        return
        [
            .. cars.Select(c => CarListItemDto.FromEntity(
                c,
                availableToday: c.Status == CarStatus.Active && !busy.Contains(c.Id))),
        ];
    }

    public async Task<CarDetailDto> GetByIdAsync(Guid carId, CancellationToken cancellationToken = default)
    {
        var car = await db.Cars
            .AsNoTracking()
            .Include(c => c.Photos)
            .FirstOrDefaultAsync(c => c.Id == carId, cancellationToken)
            ?? throw new NotFoundException(nameof(Car), carId);

        return CarDetailDto.FromEntity(car);
    }

    public async Task<CarDetailDto> CreateAsync(CreateCarRequest request, CancellationToken cancellationToken = default)
    {
        var car = Car.Create(
            request.Name,
            request.Description,
            request.Category,
            request.Seats,
            request.Rate,
            request.LicensePlate,
            request.PricingModel);

        foreach (var url in request.PhotoUrls)
        {
            car.AddPhoto(url);
        }

        db.Cars.Add(car);
        await db.SaveChangesAsync(cancellationToken);

        return CarDetailDto.FromEntity(car);
    }

    public async Task<CarDetailDto> UpdateAsync(
        Guid carId,
        UpdateCarRequest request,
        CancellationToken cancellationToken = default)
    {
        var car = await db.Cars
            .Include(c => c.Photos)
            .FirstOrDefaultAsync(c => c.Id == carId, cancellationToken)
            ?? throw new NotFoundException(nameof(Car), carId);

        car.UpdateDetails(
            request.Name,
            request.Description,
            request.Category,
            request.Seats,
            request.Rate,
            request.LicensePlate,
            request.PricingModel);

        if (request.Status is { } status)
        {
            car.ChangeStatus(status);
        }

        await db.SaveChangesAsync(cancellationToken);

        return CarDetailDto.FromEntity(car);
    }

    /// <summary>
    /// Retires a car rather than deleting it. Bookings reference cars with a
    /// restricted delete, so a hard delete would either fail or orphan history.
    /// </summary>
    public async Task RetireAsync(Guid carId, CancellationToken cancellationToken = default)
    {
        var car = await db.Cars.FirstOrDefaultAsync(c => c.Id == carId, cancellationToken)
            ?? throw new NotFoundException(nameof(Car), carId);

        car.ChangeStatus(CarStatus.Retired);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CarPhotoDto> AddPhotoAsync(
        Guid carId,
        string url,
        string? caption = null,
        bool isPrimary = false,
        CancellationToken cancellationToken = default)
    {
        var car = await db.Cars
            .Include(c => c.Photos)
            .FirstOrDefaultAsync(c => c.Id == carId, cancellationToken)
            ?? throw new NotFoundException(nameof(Car), carId);

        var photo = car.AddPhoto(url, caption, isPrimary);
        await db.SaveChangesAsync(cancellationToken);

        return CarPhotoDto.FromEntity(photo);
    }

    public async Task RemovePhotoAsync(Guid carId, Guid photoId, CancellationToken cancellationToken = default)
    {
        var car = await db.Cars
            .Include(c => c.Photos)
            .FirstOrDefaultAsync(c => c.Id == carId, cancellationToken)
            ?? throw new NotFoundException(nameof(Car), carId);

        car.RemovePhoto(photoId);
        await db.SaveChangesAsync(cancellationToken);
    }
}
