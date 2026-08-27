using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Read-only - a record of who changed what and when, including who was impersonating at the time
/// (FDD Key Entities). Admin-only.</summary>
public class AuditLogFunctions(IAuditLogRepository auditLogs, ICurrentUserAccessor currentUser)
{
    private const int MaxResults = 500;

    [Function("AuditLog_List")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "audit-log")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var userId = req.Query.TryGetValue("userId", out var u) && int.TryParse(u, out var uid) ? uid : (int?)null;
        var from = req.Query.TryGetValue("from", out var f) && DateOnly.TryParse(f, out var fd) ? fd : (DateOnly?)null;
        var to = req.Query.TryGetValue("to", out var t) && DateOnly.TryParse(t, out var td) ? td : (DateOnly?)null;

        var result = await auditLogs.GetRecentAsync(userId, from, to, MaxResults, ct);
        return new OkObjectResult(result.Select(ToDto));
    }

    private static AuditLogDto ToDto(AuditLog a) => new(
        a.Id, a.UserId, a.UserDisplayName, a.ImpersonatedUserId, a.ImpersonatedUserDisplayName,
        a.Action, a.EntityType, a.EntityId, a.Details, a.CreatedUtc);
}
