using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IStaffProjectRepository
{
    Task<StaffProject?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<StaffProject>> GetByUserIdAsync(int userId, bool activeOnly, CancellationToken ct);
    Task<IReadOnlyList<StaffProject>> GetByProjectIdAsync(int projectId, bool activeOnly, CancellationToken ct);

    /// <summary>Date-aware: "assigned" means an IsActive assignment whose range covers the given date, not
    /// merely that a row exists. This is the primary enforcement point for "a User can only log time against a
    /// Project they're assigned to" (FKs/unique-index are defense-in-depth only, they can't express the date range).</summary>
    Task<bool> IsUserAssignedAsync(int userId, int projectId, DateOnly onDate, CancellationToken ct);

    /// <summary>Range-overlap check against this (StaffId, ProjectId) pair's ACTIVE assignment only - an
    /// ended assignment (IsActive=false) imposes no restriction on new assignments, however its dates compare.
    /// A null endDate is treated as open-ended/unbounded, matching IsUserAssignedAsync's EndDate==null convention.</summary>
    Task<bool> IsOverlappingAsync(int staffId, int projectId, DateOnly startDate, DateOnly? endDate, CancellationToken ct);

    Task<IReadOnlyList<StaffProject>> GetActiveForWeekAsync(int userId, DateOnly weekStart, DateOnly weekEnd, CancellationToken ct);
    Task AddAsync(StaffProject assignment, CancellationToken ct);
    void Update(StaffProject assignment);
}
