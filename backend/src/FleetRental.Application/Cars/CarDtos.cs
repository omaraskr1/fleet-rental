using FleetRental.Domain.Entities;
using FleetRental.Domain.Enums;

namespace FleetRental.Application.Cars;

/// <summary>Listing-screen shape: only what the card renders.</summary>
public sealed record CarListItemDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Category { get; init; }

    public required int Seats { get; init; }

    public required decimal DailyRate { get; init; }

    public required string Status { get; init; }

    public string? PrimaryPhotoUrl { get; init; }

    /// <summary>
    /// Whether the car is free *today*, so the listing can show an at-a-glance
    /// badge without the client opening the calendar.
    /// </summary>
    public required bool AvailableToday { get; init; }

    public static CarListItemDto FromEntity(Car car, bool availableToday) => new()
    {
        Id = car.Id,
        Name = car.Name,
        Category = car.Category.ToString(),
        Seats = car.Seats,
        DailyRate = car.DailyRate,
        Status = car.Status.ToString(),
        PrimaryPhotoUrl = car.PrimaryPhoto?.Url,
        AvailableToday = availableToday,
    };
}

/// <summary>Detail-screen shape: adds description and the full photo set.</summary>
public sealed record CarDetailDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Category { get; init; }

    public required int Seats { get; init; }

    public required decimal DailyRate { get; init; }

    public required string Status { get; init; }

    public string? LicensePlate { get; init; }

    public required IReadOnlyList<CarPhotoDto> Photos { get; init; }

    public static CarDetailDto FromEntity(Car car) => new()
    {
        Id = car.Id,
        Name = car.Name,
        Description = car.Description,
        Category = car.Category.ToString(),
        Seats = car.Seats,
        DailyRate = car.DailyRate,
        Status = car.Status.ToString(),
        LicensePlate = car.LicensePlate,
        Photos = [.. car.Photos
            .OrderByDescending(p => p.IsPrimary)
            .ThenBy(p => p.SortOrder)
            .Select(CarPhotoDto.FromEntity)],
    };
}

public sealed record CarPhotoDto
{
    public required Guid Id { get; init; }

    public required string Url { get; init; }

    public string? Caption { get; init; }

    public required bool IsPrimary { get; init; }

    public static CarPhotoDto FromEntity(CarPhoto photo) => new()
    {
        Id = photo.Id,
        Url = photo.Url,
        Caption = photo.Caption,
        IsPrimary = photo.IsPrimary,
    };
}

public sealed record CreateCarRequest
{
    public required string Name { get; init; }

    public string Description { get; init; } = string.Empty;

    public required CarCategory Category { get; init; }

    public required int Seats { get; init; }

    public required decimal DailyRate { get; init; }

    public string? LicensePlate { get; init; }

    public IReadOnlyList<string> PhotoUrls { get; init; } = [];
}

public sealed record UpdateCarRequest
{
    public required string Name { get; init; }

    public string Description { get; init; } = string.Empty;

    public required CarCategory Category { get; init; }

    public required int Seats { get; init; }

    public required decimal DailyRate { get; init; }

    public string? LicensePlate { get; init; }

    public CarStatus? Status { get; init; }
}
