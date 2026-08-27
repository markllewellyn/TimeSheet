using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IAuditLogRepository
{
    /// <summary>Stages the add only - does not save. Callers commit it in the same
    /// uow.SaveChangesAsync(ct) as the change it's recording, so the two are atomic.</summary>
    Task AddAsync(AuditLog log, CancellationToken ct);

    Task<IReadOnlyList<AuditLog>> GetRecentAsync(int? userId, DateOnly? from, DateOnly? to, int take, CancellationToken ct);
}
