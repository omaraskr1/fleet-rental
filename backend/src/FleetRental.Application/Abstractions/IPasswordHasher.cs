namespace FleetRental.Application.Abstractions;

/// <summary>
/// Hashing lives in Infrastructure so the algorithm can be upgraded without the
/// Application or Domain layers knowing it changed.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}
