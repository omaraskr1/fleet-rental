using FleetRental.Api.Extensions;
using FleetRental.Api.Filters;
using FleetRental.Application.Availability;
using FleetRental.Application.Cars;
using FleetRental.Application.Gps;
using FleetRental.Domain.Enums;
using FleetRental.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetRental.Api.Controllers;

[ApiController]
[Route("api/cars")]
public class CarsController(CarService cars, AvailabilityService availability, GpsService gps) : ControllerBase
{
    /// <summary>
    /// The listing screen (feature 1). Requires a signed-in account — no fleet
    /// data is visible to anonymous visitors, only after login/signup.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<CarListItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CarListItemDto>>> GetAll(
        [FromQuery] CarCategory? category,
        CancellationToken ct)
    {
        // Only admins see maintenance and retired vehicles.
        var includeUnavailable = User.IsAdmin();

        return Ok(await cars.GetListAsync(includeUnavailable, category, ct));
    }

    /// <summary>The fleet-wide "where is everything right now" read for the owner's map.</summary>
    [HttpGet("locations")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [RequireFeature(FeatureKey.Gps)]
    [ProducesResponseType<IReadOnlyList<CarLocationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CarLocationDto>>> GetLocations(CancellationToken ct) =>
        Ok(await gps.GetLatestLocationsAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType<CarDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CarDetailDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await cars.GetByIdAsync(id, ct));

    /// <summary>
    /// Admin-only view of the device key, kept off <see cref="CarDetailDto"/>
    /// (which clients also read) so a booking client can never see it.
    /// </summary>
    [HttpGet("{id:guid}/gps-device-key")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [RequireFeature(FeatureKey.Gps)]
    [ProducesResponseType<GpsDeviceKeyDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GpsDeviceKeyDto>> GetGpsDeviceKey(Guid id, CancellationToken ct) =>
        Ok(await gps.GetDeviceKeyAsync(id, ct));

    /// <summary>Issues a new key, invalidating the old one — for a lost/replaced/compromised device.</summary>
    [HttpPost("{id:guid}/gps-device-key/regenerate")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [RequireFeature(FeatureKey.Gps)]
    [ProducesResponseType<GpsDeviceKeyDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GpsDeviceKeyDto>> RegenerateGpsDeviceKey(Guid id, CancellationToken ct) =>
        Ok(await gps.RegenerateDeviceKeyAsync(id, ct));

    /// <summary>Per-car availability calendar (feature 2).</summary>
    [HttpGet("{id:guid}/availability")]
    [Authorize]
    [ProducesResponseType<CarAvailabilityDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CarAvailabilityDto>> GetAvailability(
        Guid id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct) =>
        Ok(await availability.GetCarAvailabilityAsync(id, from, to, ct));

    /// <summary>
    /// Pre-submit check for the booking form. Advisory — approval re-checks under
    /// a transaction, so a stale "available" here cannot create a double-booking.
    /// </summary>
    [HttpGet("{id:guid}/availability/check")]
    [Authorize]
    [ProducesResponseType<AvailabilityCheckDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AvailabilityCheckDto>> CheckAvailability(
        Guid id,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct) =>
        Ok(await availability.CheckAsync(id, new DateRange(from, to), ct));

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType<CarDetailDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CarDetailDto>> Create(CreateCarRequest request, CancellationToken ct)
    {
        var car = await cars.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = car.Id }, car);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType<CarDetailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CarDetailDto>> Update(Guid id, UpdateCarRequest request, CancellationToken ct) =>
        Ok(await cars.UpdateAsync(id, request, ct));

    /// <summary>Retires the car. Never a hard delete — booking history references it.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Retire(Guid id, CancellationToken ct)
    {
        await cars.RetireAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/photos")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType<CarPhotoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CarPhotoDto>> AddPhoto(Guid id, AddPhotoRequest request, CancellationToken ct) =>
        Ok(await cars.AddPhotoAsync(id, request.Url, request.Caption, request.IsPrimary, ct));

    [HttpDelete("{id:guid}/photos/{photoId:guid}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemovePhoto(Guid id, Guid photoId, CancellationToken ct)
    {
        await cars.RemovePhotoAsync(id, photoId, ct);
        return NoContent();
    }
}

public sealed record AddPhotoRequest
{
    public required string Url { get; init; }

    public string? Caption { get; init; }

    public bool IsPrimary { get; init; }
}
