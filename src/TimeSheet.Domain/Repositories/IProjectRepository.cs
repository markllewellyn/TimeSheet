using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<Project>> GetByClientIdAsync(int clientId, bool includeInactive, CancellationToken ct);
    Task<IReadOnlyList<Project>> GetAssignedToUserAsync(int userId, DateOnly onDate, CancellationToken ct);
    Task<IReadOnlyList<Project>> GetAllActiveAsync(CancellationToken ct);

    /// <summary>Every project (active or not - a PM's history doesn't disappear when a project ends) nominating
    /// this user as its ProjectManager - see AuthorizationExtensions.RequireAdminOrProjectManager's call sites.</summary>
    Task<IReadOnlyList<Project>> GetManagedByUserAsync(int userId, CancellationToken ct);
    Task<bool> CodeExistsForClientAsync(int clientId, string code, int? excludeId, CancellationToken ct);
    Task AddAsync(Project project, CancellationToken ct);
    void Update(Project project);
}
