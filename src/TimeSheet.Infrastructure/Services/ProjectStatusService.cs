using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class ProjectStatusService(ITimesheetEntryRepository entries) : IProjectStatusService
{
    public async Task<ProjectStatus> GetStatusAsync(Project project, CancellationToken ct)
    {
        var actuals = await entries.GetActualsByProjectIdsAsync([project.Id], ct);
        return Build(project, actuals.GetValueOrDefault(project.Id, new ProjectActuals(0, 0)));
    }

    public async Task<IReadOnlyDictionary<int, ProjectStatus>> GetStatusesAsync(IReadOnlyCollection<Project> projects, CancellationToken ct)
    {
        if (projects.Count == 0) return new Dictionary<int, ProjectStatus>();

        var actuals = await entries.GetActualsByProjectIdsAsync(projects.Select(p => p.Id).ToList(), ct);
        return projects.ToDictionary(p => p.Id, p => Build(p, actuals.GetValueOrDefault(p.Id, new ProjectActuals(0, 0))));
    }

    private static ProjectStatus Build(Project project, ProjectActuals actuals) => new(
        project.Id,
        actuals.TotalHours, project.BudgetHours,
        project.BudgetHours is > 0 ? actuals.TotalHours / project.BudgetHours.Value * 100 : null,
        actuals.TotalCost, project.FixedFeeAmount,
        project.FixedFeeAmount is > 0 ? actuals.TotalCost / project.FixedFeeAmount.Value * 100 : null);
}
