namespace TimeSheet.Domain.Services;

/// <summary>
/// Admin-driven password management for local (non-SSO) accounts - entirely local, no external Graph call
/// needed, since these credentials live in the app's own database.
/// </summary>
public interface ILocalUserPasswordService
{
    /// <summary>Generates a temporary password, hashes and stores it against the given local user, and
    /// returns the temporary password once so the Admin can hand it to the user.</summary>
    Task<string> ResetPasswordAsync(int userId, CancellationToken ct);

    /// <summary>Generates a temporary password and its hash for a brand-new local account, before the User row
    /// exists yet (so ResetPasswordAsync's user-lookup doesn't apply) - the caller persists PasswordHash onto
    /// the new User and hands TemporaryPassword to the Admin, same as a reset.</summary>
    (string PasswordHash, string TemporaryPassword) GenerateInitialCredentials();
}
