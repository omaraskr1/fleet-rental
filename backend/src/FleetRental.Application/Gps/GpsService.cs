using System.Security.Cryptography;
using FleetRental.Application.Abstractions;
using FleetRental.Application.Common;
using FleetRental.Application.Platform;
using FleetRental.Domain.Entities;
using FleetRental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FleetRental.Application.Gps;

/// <summary>
/// Location ingestion from each car's physical GPS device, and the fleet-wide
/// "where is everything right now" read for the owner's map.
/// </summary>
public class GpsService(
    IFleetRentalDbContext db,
    ITenantContext tenantContext,
    TenantFeatureGate features,
    ILocationBroadcaster broadcaster)
{
    /// <summary>
    /// Called by the device itself — no JWT, no tenant header, just the key it
    /// was configured with. Bypasses isolation for the same reason every other
    /// key-gated, tenant-less entry point does (see <c>TenantsController.Create</c>).
    /// </summary>
    public async Task ReportLocationAsync(ReportLocationRequest request, CancellationToken cancellationToken = default)
    {
        using var _ = tenantContext.BypassIsolation();

        var car = await db.Cars.FirstOrDefaultAsync(c => c.GpsDeviceKey == request.DeviceKey, cancellationToken)
            ?? throw new AuthenticationFailedException("Unknown device key.");

        if (!await features.IsEnabledForAsync(car.TenantId, FeatureKey.Gps, cancellationToken))
        {
            throw new ForbiddenException("GPS tracking is not enabled for this company.");
        }

        var location = CarLocation.Log(
            car.Id, request.Latitude, request.Longitude, request.RecordedAt ?? DateTimeOffset.UtcNow);
        location.AssignTenant(car.TenantId);

        db.CarLocations.Add(location);
        await db.SaveChangesAsync(cancellationToken);

        await broadcaster.BroadcastAsync(
            car.TenantId,
            new CarLocationUpdate(car.Id, car.Name, location.Latitude, location.Longitude, location.RecordedAt),
            cancellationToken);
    }

    /// <summary>The latest known reading per active car in the current tenant, for the map's initial load.</summary>
    public async Task<IReadOnlyList<CarLocationDto>> GetLatestLocationsAsync(CancellationToken cancellationToken = default)
    {
        var cars = await db.Cars
            .AsNoTracking()
            .Where(c => c.Status != CarStatus.Retired)
            .ToListAsync(cancellationToken);

        var carIds = cars.Select(c => c.Id).ToList();

        // "Latest row per group" — EF Core translates this to a CROSS APPLY on
        // SQL Server rather than pulling every reading client-side.
        var latest = await db.CarLocations
            .AsNoTracking()
            .Where(l => carIds.Contains(l.CarId))
            .GroupBy(l => l.CarId)
            .Select(g => g.OrderByDescending(l => l.RecordedAt).First())
            .ToListAsync(cancellationToken);

        var carNames = cars.ToDictionary(c => c.Id, c => c.Name);

        return [.. latest.Select(l => CarLocationDto.FromEntity(l, carNames.GetValueOrDefault(l.CarId, "Unknown")))];
    }

    public async Task<GpsDeviceKeyDto> GetDeviceKeyAsync(Guid carId, CancellationToken cancellationToken = default)
    {
        var car = await db.Cars.AsNoTracking().FirstOrDefaultAsync(c => c.Id == carId, cancellationToken)
            ?? throw new NotFoundException(nameof(Car), carId);

        return new GpsDeviceKeyDto { CarId = car.Id, DeviceKey = car.GpsDeviceKey };
    }

    /// <summary>Issues a new key, replacing any existing one outright — see <c>Car.RegenerateGpsDeviceKey</c>.</summary>
    public async Task<GpsDeviceKeyDto> RegenerateDeviceKeyAsync(Guid carId, CancellationToken cancellationToken = default)
    {
        var car = await db.Cars.FirstOrDefaultAsync(c => c.Id == carId, cancellationToken)
            ?? throw new NotFoundException(nameof(Car), carId);

        var key = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        car.RegenerateGpsDeviceKey(key);
        await db.SaveChangesAsync(cancellationToken);

        return new GpsDeviceKeyDto { CarId = car.Id, DeviceKey = key };
    }
}
