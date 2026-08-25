using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
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
/// After authenticating the bearer token, this resolves the caller's app User from the Entra `oid` claim and
/// stashes it on HttpContext.Items for HttpContextCurrentUserAccessor to expose. An authenticated Entra
/// principal with no matching User row is rejected (403 "not provisioned") rather than auto-provisioned -
/// Admins pre-create User rows (invite-style); see the plan's Authentication section for the reasoning.
///
/// The one deliberate exception is the bootstrap route (see BootstrapFunctions): on a brand-new database
/// nobody is an Admin yet, so nobody could ever invite the first one. That route is let through unprovisioned
/// ONLY while the Users table is completely empty - it self-disables permanently the moment the first User
/// (the first Admin) is created.
/// </summary>
public class CurrentUserMiddleware : IFunctionsWorkerMiddleware
{
    public const string HttpContextItemKey = "CurrentUser";
    private const string BootstrapPath = "/api/bootstrap/first-admin";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext is null)
        {
            // Non-HTTP triggers (Timer, etc.) have no bearer token to validate.
            await next(context);
            return;
        }

        var authenticateResult = await httpContext.AuthenticateAsync();
        if (!authenticateResult.Succeeded)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        httpContext.User = authenticateResult.Principal;

        var oid = httpContext.User.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
            ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.User.FindFirstValue("oid");

        if (string.IsNullOrEmpty(oid))
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var users = httpContext.RequestServices.GetRequiredService<IUserRepository>();
        var user = await users.GetByEntraObjectIdAsync(oid, httpContext.RequestAborted);
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

        httpContext.Items[HttpContextItemKey] = new CurrentUserContext(user.Id, user.EntraObjectId, user.Email, user.DisplayName, user.Role);

        await next(context);
    }
}
