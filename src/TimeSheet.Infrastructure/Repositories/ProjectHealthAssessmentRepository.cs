using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class ProjectHealthAssessmentRepository(TimesheetDbContext db) : IProjectHealthAssessmentRepository
{
    public Task<ProjectHealthAssessment?> GetByIdAsync(int id, CancellationToken ct) =>
        db.ProjectHealthAssessments.Include(a => a.Project).ThenInclude(p => p!.Client).FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<ProjectHealthAssessment>> GetHistoryAsync(int projectId, CancellationToken ct)
    {
        // SQLite can't translate ORDER BY on a DateTimeOffset column - order client-side instead (result sets
        // here are small: one project's assessment history).
        var assessments = await db.ProjectHealthAssessments.Where(a => a.ProjectId == projectId).ToListAsync(ct);
        return assessments.OrderByDescending(a => a.AssessedAtUtc).ToList();
    }

    public async Task AddAsync(ProjectHealthAssessment assessment, CancellationToken ct) => await db.ProjectHealthAssessments.AddAsync(assessment, ct);
}
