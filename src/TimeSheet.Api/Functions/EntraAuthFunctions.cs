using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Server-side Entra ID sign-in - the /authorize redirect + code-exchange callback pair that replaced
/// an earlier browser-side MSAL Angular flow (AADSTS9002326: the shared app registration's redirect URI is
/// registered only under "Web", not "Single-Page Application", so a browser can't redeem the code directly).
/// Both routes are anonymous and are in CurrentUserMiddleware's bypass list - neither request ever carries our
/// own bearer token.</summary>
public class EntraAuthFunctions(IEntraAuthService entraAuth, IConfiguration configuration)
{
    private const string StateCookieName = "entra_login_state";
    private const string StateCookiePath = "/api/auth";

    [Function("Auth_EntraLogin")]
    public async Task<IActionResult> EntraLogin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/entra-login")] HttpRequest req, CancellationToken ct)
    {
        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        req.HttpContext.Response.Cookies.Append(StateCookieName, state, new CookieOptions
        {
            HttpOnly = true,
            Secure = req.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(10),
            Path = StateCookiePath,
        });

        var authorizeUrl = await entraAuth.BuildAuthorizeUrlAsync(state, ct);
        return new RedirectResult(authorizeUrl);
    }

    [Function("Auth_EntraCallback")]
    public async Task<IActionResult> EntraCallback(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/callback/microsoft-entra-id")] HttpRequest req, CancellationToken ct)
    {
        var frontendBaseUrl = configuration["Frontend:BaseUrl"]
            ?? throw new InvalidOperationException("Missing Frontend:BaseUrl configuration.");

        var expectedState = req.Cookies[StateCookieName];
        req.HttpContext.Response.Cookies.Delete(StateCookieName, new CookieOptions { Path = StateCookiePath });

        var actualState = req.Query["state"].ToString();
        var code = req.Query["code"].ToString();

        if (string.IsNullOrEmpty(expectedState) || expectedState != actualState || string.IsNullOrEmpty(code))
        {
            return new RedirectResult($"{frontendBaseUrl}/login?error=state_mismatch");
        }

        var session = await entraAuth.HandleCallbackAsync(code, ct);
        if (session is null)
        {
            return new RedirectResult($"{frontendBaseUrl}/login?error=not_provisioned");
        }

        return new RedirectResult(
            $"{frontendBaseUrl}/auth/complete#token={Uri.EscapeDataString(session.Token)}&expiresAtUtc={Uri.EscapeDataString(session.ExpiresAtUtc.ToString("O"))}");
    }
}
