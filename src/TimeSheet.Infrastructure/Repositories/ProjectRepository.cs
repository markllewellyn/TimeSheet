using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class ProjectRepository(TimesheetDbContext db) : IProjectRepository
{
    public Task<Project?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Projects.Include(p => p.Client).Include(p => p.ProjectManager).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Project>> GetByClientIdAsync(int clientId, bool includeInactive, CancellationToken ct)
    {
        var query = db.Projects.Include(p => p.ProjectManager).Where(p => p.ClientId == clientId);
        if (!includeInactive) query = query.Where(p => p.IsActive);
        return await query.OrderBy(p => p.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Project>> GetAssignedToUserAsync(int userId, DateOnly onDate, CancellationToken ct) =>
        await db.Projects
            .Include(p => p.Client)
            .Include(p => p.ProjectManager)
            .Where(p => p.Assignments.Any(a => a.StaffId == userId && a.IsActive
                        && a.StartDate <= onDate && (a.EndDate == null || a.EndDate >= onDate)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Project>> GetAllActiveAsync(CancellationToken ct) =>
        await db.Projects.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(ct);

    public Task<bool> CodeExistsForClientAsync(int clientId, string code, int? excludeId, CancellationToken ct) =>
        db.Projects.AnyAsync(p => p.ClientId == clientId && p.Code == code && (excludeId == null || p.Id != excludeId), ct);

    public async Task AddAsync(Project project, CancellationToken ct) => await db.Projects.AddAsync(project, ct);

    public void Update(Project project) => db.Projects.Update(project);
}
