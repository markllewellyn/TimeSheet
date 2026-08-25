using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IProjectHealthAssessmentRepository
{
    Task<ProjectHealthAssessment?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<ProjectHealthAssessment>> GetHistoryAsync(int projectId, CancellationToken ct);
    Task AddAsync(ProjectHealthAssessment assessment, CancellationToken ct);
}
