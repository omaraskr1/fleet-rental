using FleetRental.Domain.Entities;

namespace FleetRental.Application.Auth;

public sealed record SignUpRequest
{
    public required string Email { get; init; }

    public required string Password { get; init; }

    public required string FullName { get; init; }

    public string? PhoneNumber { get; init; }
}

public sealed record LoginRequest
{
    public required string Email { get; init; }

    public required string Password { get; init; }
}

public sealed record AuthResponse
{
    public required string AccessToken { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public required UserDto User { get; init; }
}

public sealed record UserDto
{
    public required Guid Id { get; init; }

    public required string Email { get; init; }

    public required string FullName { get; init; }

    public string? PhoneNumber { get; init; }

    public required string Role { get; init; }

    public static UserDto FromEntity(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FullName = user.FullName,
        PhoneNumber = user.PhoneNumber,
        Role = user.Role.ToString(),
    };
}

public sealed record RegisterDeviceRequest
{
    public required string Token { get; init; }

    public required Domain.Enums.DevicePlatform Platform { get; init; }

    public required string DeviceId { get; init; }
}
