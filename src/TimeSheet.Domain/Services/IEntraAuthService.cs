namespace TimeSheet.Domain.Services;

/// <summary>Server-side Entra ID sign-in via the OAuth2 authorization-code flow (a confidential-client
/// exchange using the app registration's client secret) - replaces an earlier browser-side MSAL Angular SSO
/// flow, which failed AADSTS9002326 against the shared app registration (its redirect URI is registered only
/// under the "Web" platform, not "Single-Page Application", so a browser can't redeem the code directly).
/// EntraAuthFunctions is the only caller.</summary>
public interface IEntraAuthService
{
    /// <summary>Builds the Entra /authorize URL to redirect the browser to, embedding the given CSRF state
    /// value.</summary>
    Task<string> BuildAuthorizeUrlAsync(string state, CancellationToken ct);

    /// <summary>Exchanges an authorization code for the signed-in user's identity and mints a session token
    /// for them. Null if the resolved Entra identity matches no existing (Admin-provisioned, active) User
    /// row.</summary>
    Task<LocalLoginResult?> HandleCallbackAsync(string authorizationCode, CancellationToken ct);
}
