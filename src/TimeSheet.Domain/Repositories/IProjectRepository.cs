using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<Project>> GetByClientIdAsync(int clientId, bool includeInactive, CancellationToken ct);
    Task<IReadOnlyList<Project>> GetAssignedToUserAsync(int userId, DateOnly onDate, CancellationToken ct);
    Task<IReadOnlyList<Project>> GetAllActiveAsync(CancellationToken ct);
    Task<bool> CodeExistsForClientAsync(int clientId, string code, int? excludeId, CancellationToken ct);
    Task AddAsync(Project project, CancellationToken ct);
    void Update(Project project);
}
