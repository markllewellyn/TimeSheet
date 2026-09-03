using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IProjectAttachmentRepository
{
    Task<ProjectAttachment?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<ProjectAttachment>> GetByProjectAsync(int projectId, CancellationToken ct);
    Task AddAsync(ProjectAttachment attachment, CancellationToken ct);
    void Remove(ProjectAttachment attachment);
}
