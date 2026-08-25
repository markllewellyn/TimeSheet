using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class ProjectHealthAssessmentRepository(TimesheetDbContext db) : IProjectHealthAssessmentRepository
{
    public Task<ProjectHealthAssessment?> GetByIdAsync(int id, CancellationToken ct) =>
        db.ProjectHealthAssessments.Include(a => a.Project).ThenInclude(p => p!.Client).FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<ProjectHealthAssessment>> GetHistoryAsync(int projectId, CancellationToken ct) =>
        await db.ProjectHealthAssessments
            .Where(a => a.ProjectId == projectId)
            .OrderByDescending(a => a.AssessedAtUtc)
            .ToListAsync(ct);

    public async Task AddAsync(ProjectHealthAssessment assessment, CancellationToken ct) => await db.ProjectHealthAssessments.AddAsync(assessment, ct);
}
