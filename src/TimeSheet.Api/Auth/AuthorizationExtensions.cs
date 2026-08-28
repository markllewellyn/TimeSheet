using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TimeSheet.Domain.Entities;
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

    /// <summary>Returns a 403 result unless the caller is an Admin OR is the nominated ProjectManager of the
    /// given project, otherwise null to indicate the caller may proceed. Takes an already-loaded Project
    /// (every call site needs one loaded anyway) rather than a projectId, so this stays synchronous with no
    /// repository dependency of its own - see FDD "project managers can view/query/clear" call sites.</summary>
    public static IActionResult? RequireAdminOrProjectManager(this ICurrentUserAccessor currentUser, Project project)
    {
        var user = currentUser.RequireUser();
        if (user.IsAdmin || project.ProjectManagerUserId == user.UserId) return null;
        return new ObjectResult(new { error = "Admin or project manager role required for this project." }) { StatusCode = StatusCodes.Status403Forbidden };
    }

    /// <summary>The current request's resolved app User, or throws if somehow reached an authenticated endpoint
    /// without one (CurrentUserMiddleware should have already rejected such a request with 403).</summary>
    public static CurrentUserContext RequireUser(this ICurrentUserAccessor currentUser) =>
        currentUser.Current ?? throw new InvalidOperationException("No current user resolved for an authenticated request.");
}
