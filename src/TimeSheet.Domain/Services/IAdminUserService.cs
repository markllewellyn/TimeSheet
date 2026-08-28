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

    /// <summary>FDD: "an administrator can enable or disable any account within the SVG IT tenancy" -
    /// directory lookup of tenancy members, so an admin can find someone to invite without already knowing
    /// their raw Entra Object Id. Read-only against Entra ID; never modifies the tenant account itself. A
    /// small, capped result set (this is a type-to-narrow search, not a full tenant listing).</summary>
    Task<IReadOnlyList<TenantDirectoryUser>> SearchTenantUsersAsync(string query, CancellationToken ct);
}

public record TenantDirectoryUser(string EntraObjectId, string DisplayName, string? Email);
