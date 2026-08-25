using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class ProjectAssignmentRepository(TimesheetDbContext db) : IProjectAssignmentRepository
{
    public Task<ProjectAssignment?> GetByIdAsync(int id, CancellationToken ct) =>
        db.ProjectAssignments.Include(a => a.Project).Include(a => a.User).FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<ProjectAssignment>> GetByUserIdAsync(int userId, bool activeOnly, CancellationToken ct)
    {
        var query = db.ProjectAssignments.Include(a => a.Project).ThenInclude(p => p!.Client).Where(a => a.UserId == userId);
        if (activeOnly) query = query.Where(a => a.Status == AssignmentStatus.Active);
        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProjectAssignment>> GetByProjectIdAsync(int projectId, bool activeOnly, CancellationToken ct)
    {
        var query = db.ProjectAssignments.Include(a => a.User).Where(a => a.ProjectId == projectId);
        if (activeOnly) query = query.Where(a => a.Status == AssignmentStatus.Active);
        return await query.ToListAsync(ct);
    }

    public Task<bool> IsUserAssignedAsync(int userId, int projectId, DateOnly onDate, CancellationToken ct) =>
        db.ProjectAssignments.AnyAsync(a =>
            a.UserId == userId && a.ProjectId == projectId && a.Status == AssignmentStatus.Active
            && a.StartDate <= onDate && (a.EndDate == null || a.EndDate >= onDate), ct);

    public async Task<IReadOnlyList<ProjectAssignment>> GetActiveForWeekAsync(int userId, DateOnly weekStart, DateOnly weekEnd, CancellationToken ct) =>
        await db.ProjectAssignments
            .Include(a => a.Project).ThenInclude(p => p!.Client)
            .Where(a => a.UserId == userId && a.Status == AssignmentStatus.Active
                        && a.StartDate <= weekEnd && (a.EndDate == null || a.EndDate >= weekStart))
            .ToListAsync(ct);

    public async Task AddAsync(ProjectAssignment assignment, CancellationToken ct) => await db.ProjectAssignments.AddAsync(assignment, ct);

    public void Update(ProjectAssignment assignment) => db.ProjectAssignments.Update(assignment);
}
