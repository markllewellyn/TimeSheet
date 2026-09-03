using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class StaffProjectRepository(TimesheetDbContext db) : IStaffProjectRepository
{
    public Task<StaffProject?> GetByIdAsync(int id, CancellationToken ct) =>
        db.StaffProjects.Include(sp => sp.Project).Include(sp => sp.Staff)
            .FirstOrDefaultAsync(sp => sp.Id == id, ct);

    public async Task<IReadOnlyList<StaffProject>> GetByUserIdAsync(int userId, bool activeOnly, CancellationToken ct)
    {
        var query = db.StaffProjects
            .Include(sp => sp.Project).ThenInclude(p => p!.Client)
            .Include(sp => sp.Staff)
            .Where(sp => sp.StaffId == userId);
        if (activeOnly) query = query.Where(sp => sp.IsActive);
        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StaffProject>> GetByProjectIdAsync(int projectId, bool activeOnly, CancellationToken ct)
    {
        var query = db.StaffProjects
            .Include(sp => sp.Staff)
            .Where(sp => sp.ProjectId == projectId);
        if (activeOnly) query = query.Where(sp => sp.IsActive);
        return await query.ToListAsync(ct);
    }

    public Task<bool> IsUserAssignedAsync(int userId, int projectId, DateOnly onDate, CancellationToken ct) =>
        db.StaffProjects.AnyAsync(sp =>
            sp.StaffId == userId && sp.ProjectId == projectId && sp.IsActive
            && sp.StartDate <= onDate && (sp.EndDate == null || sp.EndDate >= onDate), ct);

    public Task<bool> IsOverlappingAsync(int staffId, int projectId, DateOnly startDate, DateOnly? endDate, CancellationToken ct) =>
        db.StaffProjects.AnyAsync(sp =>
            sp.StaffId == staffId && sp.ProjectId == projectId && sp.IsActive
            && sp.StartDate <= (endDate ?? DateOnly.MaxValue) && (sp.EndDate == null || sp.EndDate >= startDate), ct);

    public async Task<IReadOnlyList<StaffProject>> GetActiveForWeekAsync(int userId, DateOnly weekStart, DateOnly weekEnd, CancellationToken ct) =>
        await db.StaffProjects
            .Include(sp => sp.Project).ThenInclude(p => p!.Client)
            .Include(sp => sp.Staff)
            .Where(sp => sp.StaffId == userId && sp.IsActive
                        && sp.StartDate <= weekEnd && (sp.EndDate == null || sp.EndDate >= weekStart))
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<int, int>> GetActiveAssignmentCountsAsync(IReadOnlyCollection<int> projectIds, CancellationToken ct) =>
        await db.StaffProjects
            .Where(sp => projectIds.Contains(sp.ProjectId) && sp.IsActive)
            .GroupBy(sp => sp.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Count, ct);

    public async Task AddAsync(StaffProject assignment, CancellationToken ct) => await db.StaffProjects.AddAsync(assignment, ct);

    public void Update(StaffProject assignment) => db.StaffProjects.Update(assignment);
}
