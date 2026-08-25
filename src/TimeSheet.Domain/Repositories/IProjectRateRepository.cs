using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IProjectRateRepository
{
    /// <summary>Most-specific-first resolution: a per-user row covering the date, else the project-default (UserId null) row covering the date, else null.</summary>
    Task<ProjectRate?> GetEffectiveRateAsync(int projectId, int? userId, DateOnly onDate, CancellationToken ct);
    Task<IReadOnlyList<ProjectRate>> GetHistoryAsync(int projectId, int? userId, CancellationToken ct);
    Task<bool> HasOverlapAsync(int projectId, int? userId, DateOnly from, DateOnly? to, int? excludeId, CancellationToken ct);
    Task AddAsync(ProjectRate rate, CancellationToken ct);
    void Update(ProjectRate rate);
}
