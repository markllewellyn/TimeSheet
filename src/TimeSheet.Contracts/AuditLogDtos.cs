namespace TimeSheet.Contracts;

public record AuditLogDto(
    int Id, int UserId, string UserDisplayName, int? ImpersonatedUserId, string? ImpersonatedUserDisplayName,
    string Action, string EntityType, int? EntityId, string? Details, DateTimeOffset CreatedUtc);
