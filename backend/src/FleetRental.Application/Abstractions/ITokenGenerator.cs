using FleetRental.Domain.Entities;

namespace FleetRental.Application.Abstractions;

public interface ITokenGenerator
{
    /// <summary>Issues a signed JWT carrying the user's id and role.</summary>
    AuthToken Generate(User user);
}

/// <param name="AccessToken">Signed JWT for the Authorization header.</param>
/// <param name="ExpiresAt">When the token stops being accepted.</param>
public readonly record struct AuthToken(string AccessToken, DateTimeOffset ExpiresAt);
