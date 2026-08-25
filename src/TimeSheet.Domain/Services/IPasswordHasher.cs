namespace TimeSheet.Domain.Services;

/// <summary>Hashes/verifies passwords for local (non-SSO) accounts stored in the app's own database.</summary>
public interface IPasswordHasher
{
    /// <summary>Self-contained: algorithm identifier, iteration count, salt, and hash are all encoded in the
    /// single returned string, so the format can evolve later without a schema change.</summary>
    string Hash(string password);

    bool Verify(string password, string hash);
}
