using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TimeSheet.Api.Auth;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Admin account maintenance against Entra ID itself (via Graph) - separate from the Users_* CRUD
/// endpoints, which only manage the app's own business-profile records.</summary>
public class AdminUsersFunctions(IUserRepository users, IAdminUserService adminUserService, ICurrentUserAccessor currentUser, ILogger<AdminUsersFunctions> logger)
{
    [Function("AdminUsers_ResetPassword")]
    public async Task<IActionResult> ResetPassword(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "users/{id:int}/reset-password")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var targetUser = await users.GetByIdAsync(id, ct);
        if (targetUser is null) return new NotFoundResult();

        var temporaryPassword = await adminUserService.ForcePasswordResetAsync(targetUser.EntraObjectId, ct);

        // Audit trail: who reset whose password, when. A dedicated AuditLog table would be a natural future
        // enhancement; structured logging is the audit record for now.
        logger.LogWarning(
            "Admin {AdminUserId} force-reset the password for user {TargetUserId} ({TargetEmail}) at {TimestampUtc}",
            currentUser.RequireUser().UserId, targetUser.Id, targetUser.Email, DateTimeOffset.UtcNow);

        return new OkObjectResult(new { temporaryPassword });
    }
}
