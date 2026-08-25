using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IProjectAssignmentRepository
{
    Task<ProjectAssignment?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<ProjectAssignment>> GetByUserIdAsync(int userId, bool activeOnly, CancellationToken ct);
    Task<IReadOnlyList<ProjectAssignment>> GetByProjectIdAsync(int projectId, bool activeOnly, CancellationToken ct);

    /// <summary>Date-aware: "assigned" means an Active assignment whose range covers the given date, not merely
    /// that a row exists. This is the primary enforcement point for "a User can only log time against a Project
    /// they're assigned to" (FKs/unique-index are defense-in-depth only, they can't express the date range).</summary>
    Task<bool> IsUserAssignedAsync(int userId, int projectId, DateOnly onDate, CancellationToken ct);

    Task<IReadOnlyList<ProjectAssignment>> GetActiveForWeekAsync(int userId, DateOnly weekStart, DateOnly weekEnd, CancellationToken ct);
    Task AddAsync(ProjectAssignment assignment, CancellationToken ct);
    void Update(ProjectAssignment assignment);
}
