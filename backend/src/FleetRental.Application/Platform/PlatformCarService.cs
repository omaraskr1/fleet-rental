using FleetRental.Application.Abstractions;
using FleetRental.Application.Cars;
using FleetRental.Application.Common;
using FleetRental.Domain.Entities;
using FleetRental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FleetRental.Application.Platform;

/// <summary>
/// Cross-company car monitoring and management for the platform panel. Bypasses
/// isolation for the same reason <see cref="PlatformCompanyService"/> does — a
/// platform admin's token carries no tenant to filter by, and here that's the
/// point: every company's fleet in one screen.
/// </summary>
public class PlatformCarService(IFleetRentalDbContext db, ITenantContext tenantContext)
{
    public async Task<IReadOnlyList<PlatformCarDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var _ = tenantContext.BypassIsolation();

        var cars = await db.Cars
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var companyNames = await CompanyNamesAsync(cancellationToken);

        return
        [
            .. cars
                .OrderBy(c => companyNames.GetValueOrDefault(c.TenantId, string.Empty))
                .ThenBy(c => c.Name)
                .Select(c => PlatformCarDto.FromEntity(c, companyNames.GetValueOrDefault(c.TenantId, "Unknown"))),
        ];
    }

    public async Task<PlatformCarDto> CreateAsync(
        CreatePlatformCarRequest request,
        CancellationToken cancellationToken = default)
    {
        using var _ = tenantContext.BypassIsolation();

        var company = await db.Tenants.FirstOrDefaultAsync(t => t.Id == request.CompanyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), request.CompanyId);

        var car = Car.Create(
            request.Name,
            request.Description,
            request.Category,
            request.Seats,
            request.Rate,
            request.LicensePlate,
            request.PricingModel);

        car.AssignTenant(request.CompanyId);

        db.Cars.Add(car);
        await db.SaveChangesAsync(cancellationToken);

        return PlatformCarDto.FromEntity(car, company.Name);
    }

    public async Task<PlatformCarDto> UpdateAsync(
        Guid carId,
        UpdateCarRequest request,
        CancellationToken cancellationToken = default)
    {
        using var _ = tenantContext.BypassIsolation();

        var car = await db.Cars.FirstOrDefaultAsync(c => c.Id == carId, cancellationToken)
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

        var companyNames = await CompanyNamesAsync(cancellationToken);
        return PlatformCarDto.FromEntity(car, companyNames.GetValueOrDefault(car.TenantId, "Unknown"));
    }

    /// <summary>Retires the car — never a hard delete, same rule as <c>CarService</c>.</summary>
    public async Task RetireAsync(Guid carId, CancellationToken cancellationToken = default)
    {
        using var _ = tenantContext.BypassIsolation();

        var car = await db.Cars.FirstOrDefaultAsync(c => c.Id == carId, cancellationToken)
            ?? throw new NotFoundException(nameof(Car), carId);

        car.ChangeStatus(CarStatus.Retired);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, string>> CompanyNamesAsync(CancellationToken cancellationToken) =>
        await db.Tenants.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);
}
