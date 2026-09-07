using System.Security.Claims;
using Microsoft.Identity.Client;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class EntraAuthService(
    IConfidentialClientApplication msalClient,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ILocalAuthService localAuth) : IEntraAuthService
{
    // Pure sign-in - no delegated Graph call is ever made with this token, so no Graph scope is requested.
    // MSAL.NET always merges in the reserved "openid"/"profile"/"offline_access" scopes regardless, which is
    // exactly (and only) what's needed to get an ID token back.
    private static readonly string[] Scopes = [];

    public async Task<string> BuildAuthorizeUrlAsync(string state, CancellationToken ct)
    {
        var uri = await msalClient.GetAuthorizationRequestUrl(Scopes)
            .WithExtraQueryParameters(new Dictionary<string, (string Value, bool IncludeInCacheKey)>
            {
                ["state"] = (state, false),
                ["response_mode"] = ("query", false),
            })
            .ExecuteAsync(ct);

        return uri.ToString();
    }

    public async Task<LocalLoginResult?> HandleCallbackAsync(string authorizationCode, CancellationToken ct)
    {
        var result = await msalClient.AcquireTokenByAuthorizationCode(Scopes, authorizationCode).ExecuteAsync(ct);

        var oid = result.ClaimsPrincipal.FindFirst("oid")?.Value;
        if (string.IsNullOrEmpty(oid)) return null;

        var user = await users.GetByEntraObjectIdAsync(oid, ct);

        // First-time SSO login for an existing local (Admin-created) account - same auto-link-by-email rule as
        // CurrentUserMiddleware's per-request Entra path (only links a row whose EntraObjectId is still null,
        // never creates a new one). Deliberately duplicated here rather than shared, to avoid any risk to that
        // already-working, currently-exercised code path.
        if (user is null)
        {
            var email = result.ClaimsPrincipal.FindFirst("preferred_username")?.Value
                ?? result.ClaimsPrincipal.FindFirst("email")?.Value
                ?? result.ClaimsPrincipal.FindFirst(ClaimTypes.Upn)?.Value;
            if (!string.IsNullOrEmpty(email))
            {
                var byEmail = await users.GetByEmailAsync(email, ct);
                if (byEmail is not null && byEmail.EntraObjectId is null)
                {
                    byEmail.EntraObjectId = oid;
                    users.Update(byEmail);
                    await unitOfWork.SaveChangesAsync(ct);
                    user = byEmail;
                }
            }
        }

        if (user is null || !user.IsActive) return null;

        return localAuth.IssueSessionToken(user);
    }
}
