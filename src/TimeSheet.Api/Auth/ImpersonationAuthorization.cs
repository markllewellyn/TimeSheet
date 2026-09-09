using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Auth;

/// <summary>Shared "Admin may act on behalf of another active user" gating - the FDD's own two rules ("only
/// active users can be impersonated and only admins can impersonate users") in one place, rather than
/// duplicated per endpoint. Used by every endpoint that accepts an OnBehalfOfUserId - timesheet entries,
/// expense entries, and the personal overview.</summary>
public static class ImpersonationAuthorization
{
    /// <summary>Query-string form of impersonation context, for endpoints (Get/List/Delete) that have no JSON
    /// body to carry OnBehalfOfUserId on.</summary>
    public static int? ParseOnBehalfOfUserId(HttpRequest req) =>
        req.Query.TryGetValue("onBehalfOfUserId", out var v) && int.TryParse(v, out var id) ? id : null;

    /// <summary>A "whose data" view gate - an Admin may view another user's own data by supplying their id;
    /// anyone else is confined to their own.</summary>
    public static async Task<(IActionResult? Error, int EffectiveUserId)> ResolveViewTargetAsync(
        IUserRepository users, CurrentUserContext user, int? onBehalfOfUserId, CancellationToken ct)
    {
        if (onBehalfOfUserId is { } id && id != user.UserId)
        {
            if (!user.IsAdmin)
            {
                return (new ObjectResult(new { error = "Admin role required to view another user's data." })
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                }, 0);
            }
            if (await ValidateImpersonationTargetAsync(users, id, ct) is { } impersonationError) return (impersonationError, 0);
            return (null, id);
        }
        return (null, user.UserId);
    }

    /// <summary>FDD: only an active user can be impersonated (logged-for, edited-for, or viewed-as-if) - checked
    /// server-side here rather than only in the Angular impersonation picker's active-only filter, matching this
    /// codebase's general "lock in the API, not only the UI" pattern.</summary>
    public static async Task<IActionResult?> ValidateImpersonationTargetAsync(IUserRepository users, int onBehalfOfUserId, CancellationToken ct)
    {
        var target = await users.GetByIdAsync(onBehalfOfUserId, ct);
        if (target is null || !target.IsActive)
        {
            return new ObjectResult(new { error = "Only an active user can be impersonated." })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
        }
        return null;
    }

    /// <summary>Get/Update/Delete/Duplicate's ownership gate - the caller owns the record outright, or is an
    /// Admin actively impersonating its owner (onBehalfOfUserId must equal the record's own UserId). Returns the
    /// impersonated user id for AuditLog when the second branch is what authorized the call, so a plain self-
    /// edit never gets tagged as impersonation.</summary>
    public static (bool Authorized, int? ImpersonatedUserId) CheckOwnership(int recordOwnerUserId, CurrentUserContext user, int? onBehalfOfUserId)
    {
        if (recordOwnerUserId == user.UserId) return (true, null);
        if (user.IsAdmin && onBehalfOfUserId == recordOwnerUserId) return (true, recordOwnerUserId);
        return (false, null);
    }
}
