using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class ProjectRepository(TimesheetDbContext db) : IProjectRepository
{
    public Task<Project?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Projects.Include(p => p.Rates).Include(p => p.Client).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Project>> GetByClientIdAsync(int clientId, bool includeInactive, CancellationToken ct)
    {
        var query = db.Projects.Where(p => p.ClientId == clientId);
        if (!includeInactive) query = query.Where(p => p.IsActive);
        return await query.OrderBy(p => p.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Project>> GetAssignedToUserAsync(int userId, DateOnly onDate, CancellationToken ct) =>
        await db.ProjectAssignments
            .Where(a => a.UserId == userId && a.Status == AssignmentStatus.Active
                        && a.StartDate <= onDate && (a.EndDate == null || a.EndDate >= onDate))
            .Select(a => a.Project!)
            .Distinct()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Project>> GetAllActiveAsync(CancellationToken ct) =>
        await db.Projects.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(ct);

    public Task<bool> CodeExistsForClientAsync(int clientId, string code, int? excludeId, CancellationToken ct) =>
        db.Projects.AnyAsync(p => p.ClientId == clientId && p.Code == code && (excludeId == null || p.Id != excludeId), ct);

    public async Task AddAsync(Project project, CancellationToken ct) => await db.Projects.AddAsync(project, ct);

    public void Update(Project project) => db.Projects.Update(project);
}
