using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IAttachmentRepository
{
    Task<Attachment?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<Attachment>> GetByTimesheetEntryAsync(int timesheetEntryId, CancellationToken ct);
    Task AddAsync(Attachment attachment, CancellationToken ct);
    void Remove(Attachment attachment);
}
