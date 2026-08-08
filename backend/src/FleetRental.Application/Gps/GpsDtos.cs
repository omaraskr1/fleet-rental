using FleetRental.Domain.Entities;

namespace FleetRental.Application.Gps;

public sealed record ReportLocationRequest
{
    public required string DeviceKey { get; init; }

    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    /// <summary>When the device took the reading. Defaults to server-received time if omitted.</summary>
    public DateTimeOffset? RecordedAt { get; init; }
}

public sealed record CarLocationDto
{
    public required Guid CarId { get; init; }

    public required string CarName { get; init; }

    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    public required DateTimeOffset RecordedAt { get; init; }

    public static CarLocationDto FromEntity(CarLocation location, string carName) => new()
    {
        CarId = location.CarId,
        CarName = carName,
        Latitude = location.Latitude,
        Longitude = location.Longitude,
        RecordedAt = location.RecordedAt,
    };
}

/// <summary>
/// Deliberately its own shape, never folded into <see cref="Cars.CarDetailDto"/>
/// — that DTO is also read by clients browsing/booking a car, and a device key
/// must never appear in a response a client can see.
/// </summary>
public sealed record GpsDeviceKeyDto
{
    public required Guid CarId { get; init; }

    public string? DeviceKey { get; init; }
}
