using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class AuditLogService(IAuditLogRepository auditLogs, IUserRepository users) : IAuditLogService
{
    public async Task LogAsync(CurrentUserContext actor, string action, string entityType, int? entityId, string? details, int? impersonatedUserId, CancellationToken ct)
    {
        string? impersonatedDisplayName = null;
        if (impersonatedUserId is { } id)
        {
            var impersonated = await users.GetByIdAsync(id, ct);
            impersonatedDisplayName = impersonated?.DisplayName;
        }

        var log = new AuditLog
        {
            UserId = actor.UserId,
            UserDisplayName = actor.DisplayName,
            ImpersonatedUserId = impersonatedUserId,
            ImpersonatedUserDisplayName = impersonatedDisplayName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        await auditLogs.AddAsync(log, ct);
    }
}
