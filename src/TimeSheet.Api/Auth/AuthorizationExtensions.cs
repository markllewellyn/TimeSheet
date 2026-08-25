using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Auth;

public static class AuthorizationExtensions
{
    /// <summary>Returns a 403 result if the caller isn't an Admin (per the app's own User.Role), otherwise null
    /// to indicate the caller may proceed. Endpoint-level authorization is checked against the app's database,
    /// not Entra App Roles - see CurrentUserMiddleware.</summary>
    public static IActionResult? RequireAdmin(this ICurrentUserAccessor currentUser)
    {
        if (currentUser.Current is not { IsAdmin: true })
        {
            return new ObjectResult(new { error = "Admin role required." }) { StatusCode = StatusCodes.Status403Forbidden };
        }
        return null;
    }

    /// <summary>The current request's resolved app User, or throws if somehow reached an authenticated endpoint
    /// without one (CurrentUserMiddleware should have already rejected such a request with 403).</summary>
    public static CurrentUserContext RequireUser(this ICurrentUserAccessor currentUser) =>
        currentUser.Current ?? throw new InvalidOperationException("No current user resolved for an authenticated request.");
}
