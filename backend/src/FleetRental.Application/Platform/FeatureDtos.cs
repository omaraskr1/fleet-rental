using FleetRental.Domain.Enums;

namespace FleetRental.Application.Platform;

public sealed record FeatureToggleDto
{
    public required FeatureKey Key { get; init; }

    public required bool IsEnabled { get; init; }
}

public sealed record SetFeatureToggleRequest
{
    public required bool IsEnabled { get; init; }
}
