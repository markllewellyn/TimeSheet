namespace TimeSheet.Domain.Services;

public interface IAuditLogService
{
    /// <summary>Stages an AuditLog row (does not save) - callers commit it in the same uow.SaveChangesAsync(ct)
    /// as the change it's recording, so the two are atomic. impersonatedUserId is set only when actor performed
    /// the action while impersonating that user (see TimesheetEntriesFunctions' onBehalfOfUserId handling).</summary>
    Task LogAsync(CurrentUserContext actor, string action, string entityType, int? entityId, string? details, int? impersonatedUserId, CancellationToken ct);
}
