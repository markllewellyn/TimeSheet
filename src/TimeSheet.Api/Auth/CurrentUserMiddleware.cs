using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Auth;

/// <summary>
/// Azure Functions isolated-worker middleware runs as its own message pipeline (not a conventional ASP.NET Core
/// IApplicationBuilder pipeline), so authentication/authorization here is invoked manually against the
/// per-invocation HttpContext (context.GetHttpContext()) rather than via app.UseAuthentication()/UseAuthorization().
///
/// Tries the Entra ("Bearer") scheme first, then falls back to the "LocalBearer" scheme for local (username/
/// password) accounts - this is what lets a local account sign in even if Entra SSO is unreachable/
/// misconfigured. After authenticating, resolves the caller's app User (by EntraObjectId for an Entra token,
/// by the app's own User.Id for a local token) and stashes it on HttpContext.Items for
/// HttpContextCurrentUserAccessor to expose. An authenticated principal with no matching User row is rejected
/// (403 "not provisioned") rather than auto-provisioned - Admins pre-create User rows (invite-style); see the
/// plan's Authentication section for the reasoning. The one exception: an Entra token whose oid matches no
/// User but whose verified email claim matches an existing unlinked (EntraObjectId == null) row auto-links
/// that row to the oid on this first SSO login, rather than requiring a separate manual linking step - this
/// still never creates a new User, only attaches an Entra identity to one an Admin already provisioned.
///
/// There are two deliberate exceptions, both on a brand-new database only (nobody is an Admin yet, so nobody
/// could ever invite the first one otherwise), both self-disabling permanently the moment the first User is
/// created:
///  - The Entra bootstrap route (see BootstrapFunctions.FirstAdmin) is let through unprovisioned but still
///    requires a valid Entra/local token.
///  - The local bootstrap route (BootstrapFunctions.FirstLocalAdmin) requires no token at all - it's the
///    fallback path when Entra SSO isn't set up/working yet, so it can't require an Entra token itself.
/// A third, always-on exception: the local-login route itself (AuthFunctions.LocalLogin) must be reachable
/// with no token, at any time - it's the endpoint that ISSUES the token, so requiring one first would make it
/// impossible to ever call.
/// </summary>
public class CurrentUserMiddleware : IFunctionsWorkerMiddleware
{
    public const string HttpContextItemKey = "CurrentUser";
    private const string BootstrapPath = "/api/bootstrap/first-admin";
    private const string LocalBootstrapPath = "/api/bootstrap/first-local-admin";
    private const string LocalLoginPath = "/api/auth/local-login";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext is null)
        {
            // Non-HTTP triggers (Timer, etc.) have no bearer token to validate.
            await next(context);
            return;
        }

        // The isolated-worker host doesn't run the conventional ASP.NET Core pipeline component that normally
        // populates this automatically, so nothing else ever assigns it - without this line,
        // HttpContextCurrentUserAccessor (resolved via DI inside the function) sees a null HttpContext even
        // though this middleware's own httpContext.Items assignment below succeeded.
        httpContext.RequestServices.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

        if (httpContext.Request.Path.Equals(LocalLoginPath, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (httpContext.Request.Path.Equals(LocalBootstrapPath, StringComparison.OrdinalIgnoreCase))
        {
            var usersForLocalBootstrap = httpContext.RequestServices.GetRequiredService<IUserRepository>();
            if ((await usersForLocalBootstrap.GetAllAsync(includeInactive: true, httpContext.RequestAborted)).Count == 0)
            {
                await next(context);
                return;
            }

            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await httpContext.Response.WriteAsJsonAsync(new { error = "Bootstrap is only available before the first user has been created." });
            return;
        }

        var authenticateResult = await httpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        var isLocalToken = false;
        if (!authenticateResult.Succeeded)
        {
            authenticateResult = await httpContext.AuthenticateAsync(LocalAuthConstants.SchemeName);
            isLocalToken = authenticateResult.Succeeded;
        }

        if (!authenticateResult.Succeeded)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        httpContext.User = authenticateResult.Principal;

        var users = httpContext.RequestServices.GetRequiredService<IUserRepository>();
        Domain.Entities.User? user;

        if (isLocalToken)
        {
            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            user = int.TryParse(userIdClaim, out var userId) ? await users.GetByIdAsync(userId, httpContext.RequestAborted) : null;
        }
        else
        {
            var oid = httpContext.User.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
                ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("oid");
            user = string.IsNullOrEmpty(oid) ? null : await users.GetByEntraObjectIdAsync(oid, httpContext.RequestAborted);

            // First-time SSO login for an existing local (Admin-created) account: no User row is linked to this
            // oid yet, but the token's verified email claim matches an existing unlinked (EntraObjectId == null)
            // User row - link it now rather than requiring a separate "link my account" step. The email comes
            // from the token issued by the tenant configured in AzureAd:TenantId, so it's as trustworthy as the
            // oid itself; only ever auto-links an account that has no Entra identity attached yet.
            if (user is null && !string.IsNullOrEmpty(oid))
            {
                var email = httpContext.User.FindFirstValue(ClaimTypes.Upn)
                    ?? httpContext.User.FindFirstValue("preferred_username")
                    ?? httpContext.User.FindFirstValue(ClaimTypes.Email);
                if (!string.IsNullOrEmpty(email))
                {
                    var byEmail = await users.GetByEmailAsync(email, httpContext.RequestAborted);
                    if (byEmail is not null && byEmail.EntraObjectId is null)
                    {
                        byEmail.EntraObjectId = oid;
                        users.Update(byEmail);
                        await httpContext.RequestServices.GetRequiredService<IUnitOfWork>().SaveChangesAsync(httpContext.RequestAborted);
                        user = byEmail;
                    }
                }
            }
        }

        if (user is null || !user.IsActive)
        {
            if (httpContext.Request.Path.Equals(BootstrapPath, StringComparison.OrdinalIgnoreCase)
                && (await users.GetAllAsync(includeInactive: true, httpContext.RequestAborted)).Count == 0)
            {
                // No CurrentUserContext is set - the bootstrap function reads the raw Entra claims itself.
                await next(context);
                return;
            }

            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await httpContext.Response.WriteAsJsonAsync(new { error = "not provisioned" });
            return;
        }

        httpContext.Items[HttpContextItemKey] = new CurrentUserContext(user.Id, user.EntraObjectId ?? "", user.Email, user.DisplayName, user.Role);

        await next(context);
    }
}
