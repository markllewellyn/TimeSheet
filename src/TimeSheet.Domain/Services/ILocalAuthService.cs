namespace TimeSheet.Domain.Services;

/// <summary>Shared between Program.cs (registers the "LocalBearer" JWT scheme), CurrentUserMiddleware (tries
/// it as a fallback to the Entra scheme), and LocalAuthService (issues tokens for it) - kept in Domain since
/// Infrastructure can't reference the Api project.</summary>
public static class LocalAuthConstants
{
    public const string SchemeName = "LocalBearer";
    public const string Issuer = "TimeSheetApp";
    public const string Audience = "TimeSheetApp";
}

public record LocalLoginResult(string Token, DateTimeOffset ExpiresAtUtc);

/// <summary>
/// Username/password sign-in for local accounts (User.PasswordHash set, no Entra identity) - a first-class
/// account type for people without an Entra identity in this tenant, and a fallback sign-in path if Entra SSO
/// is ever misconfigured.
/// </summary>
public interface ILocalAuthService
{
    /// <summary>Null if the email/password don't match an active local account.</summary>
    Task<LocalLoginResult?> LoginAsync(string email, string password, CancellationToken ct);
}
