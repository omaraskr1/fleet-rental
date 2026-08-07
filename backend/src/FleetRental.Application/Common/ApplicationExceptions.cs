namespace FleetRental.Application.Common;

/// <summary>Requested entity does not exist. Mapped to 404 by the API.</summary>
public class NotFoundException(string entity, object key)
    : Exception($"{entity} '{key}' was not found.");

/// <summary>Caller is authenticated but may not touch this resource. Mapped to 403.</summary>
public class ForbiddenException(string message) : Exception(message);

/// <summary>Credentials rejected. Mapped to 401.</summary>
public class AuthenticationFailedException(string message) : Exception(message);

/// <summary>
/// The request collided with another. Mapped to 409 — used when a booking's dates
/// were taken between the client seeing availability and the admin approving.
/// </summary>
public class ConflictException(string message) : Exception(message);

/// <summary>Input failed validation. Mapped to 400 with per-field detail.</summary>
public class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public static ValidationException Single(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
