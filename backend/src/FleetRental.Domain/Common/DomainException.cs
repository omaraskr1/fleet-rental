namespace FleetRental.Domain.Common;

/// <summary>
/// Raised when an operation would violate a domain invariant. The API layer maps
/// this to a 400/409 rather than letting it surface as a 500.
/// </summary>
public class DomainException(string message) : Exception(message);
