using FleetRental.Application.Cars;
using FleetRental.Domain.Entities;
using FleetRental.Domain.Enums;

namespace FleetRental.Application.Platform;

/// <summary>Same shape as <see cref="CarDetailDto"/>, plus which company owns it.</summary>
public sealed record PlatformCarDto
{
    public required Guid Id { get; init; }

    public required Guid CompanyId { get; init; }

    public required string CompanyName { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Category { get; init; }

    public required int Seats { get; init; }

    public required decimal Rate { get; init; }

    public required string PricingModel { get; init; }

    public required string Status { get; init; }

    public string? LicensePlate { get; init; }

    public static PlatformCarDto FromEntity(Car car, string companyName) => new()
    {
        Id = car.Id,
        CompanyId = car.TenantId,
        CompanyName = companyName,
        Name = car.Name,
        Description = car.Description,
        Category = car.Category.ToString(),
        Seats = car.Seats,
        Rate = car.Rate,
        PricingModel = car.PricingModel.ToString(),
        Status = car.Status.ToString(),
        LicensePlate = car.LicensePlate,
    };
}

public sealed record CreatePlatformCarRequest
{
    public required Guid CompanyId { get; init; }

    public required string Name { get; init; }

    public string Description { get; init; } = string.Empty;

    public required CarCategory Category { get; init; }

    public required int Seats { get; init; }

    public required decimal Rate { get; init; }

    public PricingModel PricingModel { get; init; } = PricingModel.PerDay;

    public string? LicensePlate { get; init; }
}
