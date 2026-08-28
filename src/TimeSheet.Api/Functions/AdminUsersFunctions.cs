using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Admin account maintenance - separate from the Users_* CRUD endpoints, which only manage the app's
/// own business-profile records. Branches by account type: an SSO account's password lives in Entra ID and is
/// reset via Graph; a local account's password lives in this app's own database and is reset directly.</summary>
public class AdminUsersFunctions(
    IUserRepository users,
    IAdminUserService adminUserService,
    ILocalUserPasswordService localUserPasswordService,
    ICurrentUserAccessor currentUser,
    ILogger<AdminUsersFunctions> logger)
{
    [Function("AdminUsers_ResetPassword")]
    public async Task<IActionResult> ResetPassword(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "users/{id:int}/reset-password")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var targetUser = await users.GetByIdAsync(id, ct);
        if (targetUser is null) return new NotFoundResult();

        var temporaryPassword = targetUser.IsLocalAccount
            ? await localUserPasswordService.ResetPasswordAsync(targetUser.Id, ct)
            : await adminUserService.ForcePasswordResetAsync(targetUser.EntraObjectId!, ct);

        // Audit trail: who reset whose password, when. A dedicated AuditLog table would be a natural future
        // enhancement; structured logging is the audit record for now.
        logger.LogWarning(
            "Admin {AdminUserId} force-reset the password for user {TargetUserId} ({TargetEmail}, {AccountType}) at {TimestampUtc}",
            currentUser.RequireUser().UserId, targetUser.Id, targetUser.Email, targetUser.IsLocalAccount ? "local" : "SSO", DateTimeOffset.UtcNow);

        return new OkObjectResult(new { temporaryPassword });
    }

    /// <summary>FDD: "an administrator can enable or disable any account within the SVG IT tenancy" -
    /// directory lookup so an admin can find a not-yet-invited tenant member instead of already knowing their
    /// raw Entra Object Id. Read-only against Entra ID - see IAdminUserService.SearchTenantUsersAsync.</summary>
    [Function("AdminUsers_SearchTenantDirectory")]
    public async Task<IActionResult> SearchTenantDirectory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenant-directory/search")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var query = req.Query["q"].ToString();
        if (query.Trim().Length < 2)
        {
            return new BadRequestObjectResult(new { error = "Enter at least 2 characters to search." });
        }

        var matches = await adminUserService.SearchTenantUsersAsync(query.Trim(), ct);

        var dtos = new List<TenantDirectoryUserDto>();
        foreach (var m in matches)
        {
            var existing = await users.GetByEntraObjectIdAsync(m.EntraObjectId, ct);
            dtos.Add(new TenantDirectoryUserDto(m.EntraObjectId, m.DisplayName, m.Email, existing is not null, existing?.Id, existing?.IsActive));
        }
        return new OkObjectResult(dtos);
    }
}
