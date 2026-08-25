namespace TimeSheet.Domain.Services;

/// <summary>
/// Admin panel account maintenance against Entra ID itself (via Microsoft Graph) - distinct from the app's own
/// User table, which only holds business profile data (Role, display info) linked by EntraObjectId.
/// </summary>
public interface IAdminUserService
{
    /// <summary>Generates a temporary password and forces a change at next sign-in, returning the temporary
    /// password once so the admin can hand it to the user. Both the password and forceChangePasswordNextSignIn
    /// must be sent together in the same Graph call - setting the flag alone does not reliably take effect.</summary>
    Task<string> ForcePasswordResetAsync(string entraObjectId, CancellationToken ct);
}
