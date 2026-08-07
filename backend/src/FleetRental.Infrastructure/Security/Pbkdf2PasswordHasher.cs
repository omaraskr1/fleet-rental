using System.Security.Cryptography;
using FleetRental.Application.Abstractions;

namespace FleetRental.Infrastructure.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 with a per-password random salt.
/// </summary>
/// <remarks>
/// Format is <c>{iterations}.{base64 salt}.{base64 hash}</c>. Embedding the
/// iteration count means the work factor can be raised later without invalidating
/// existing passwords — old hashes keep verifying at their original count.
/// </remarks>
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int DefaultIterations = 210_000;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, DefaultIterations, HashAlgorithmName.SHA256, HashSize);

        return $"{DefaultIterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        var parts = hash.Split('.', 3);

        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        byte[] salt, expected;

        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expected = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        // Constant-time: a plain == would leak how much of the hash matched via timing.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
