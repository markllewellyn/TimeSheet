using System.Security.Cryptography;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

/// <summary>Encoded as "pbkdf2.{iterations}.{base64 salt}.{base64 hash}" - self-contained so the format/cost
/// can change later without a schema migration (Verify branches on the algorithm tag).</summary>
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 210_000; // OWASP-recommended minimum for PBKDF2-SHA256 as of the 2020s.
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSizeBytes);
        return $"pbkdf2.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('.');
        if (parts.Length != 4 || parts[0] != "pbkdf2") return false;
        if (!int.TryParse(parts[1], out var iterations)) return false;

        var salt = Convert.FromBase64String(parts[2]);
        var expectedHash = Convert.FromBase64String(parts[3]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
