using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class ProjectEstimateService(
    IProjectRepository projects,
    IStaffProjectRepository staffProjects,
    IRoleRepository roles,
    IRateResolver rateResolver) : IProjectEstimateService
{
    public async Task<ProjectEstimate> EstimateAsync(int projectId, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(projectId, ct)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        var assignments = await staffProjects.GetByProjectIdAsync(projectId, activeOnly: true, ct);
        if (project.BudgetHours is null || assignments.Count == 0)
        {
            return new ProjectEstimate(projectId, project.BudgetHours, 0, 0, 0, []);
        }

        // Split BudgetHours across assignees weighted by AllocatedHoursPerWeek; fall back to an equal split
        // when nobody has one set (there's no separate "allotted hours" input on an assignment).
        var totalWeight = assignments.Sum(a => a.AllocatedHoursPerWeek ?? 0);
        var equalShare = project.BudgetHours.Value / assignments.Count;

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
        var lines = new List<ProjectEstimateLine>();

        foreach (var assignment in assignments)
        {
            var staff = assignment.Staff!;
            var share = totalWeight > 0
                ? project.BudgetHours.Value * ((assignment.AllocatedHoursPerWeek ?? 0) / totalWeight)
                : equalShare;

            var role = staff.JobRoleId is { } roleId ? await roles.GetByIdAsync(roleId, ct) : null;

            try
            {
                var resolution = await rateResolver.ResolveAsync(staff.Id, project.ClientId, projectId, today, project.IsCostExempt, ct);
                var cost = share * resolution.HourlyCost;
                var revenue = share * resolution.CustomerRate;
                lines.Add(new ProjectEstimateLine(
                    staff.Id, staff.DisplayName, staff.JobRoleId, role?.Name, share,
                    resolution.HourlyCost, resolution.CustomerRate, cost, revenue, revenue - cost, null));
            }
            catch (RateNotConfiguredException ex)
            {
                lines.Add(new ProjectEstimateLine(staff.Id, staff.DisplayName, staff.JobRoleId, role?.Name, share, null, null, 0, 0, 0, ex.Message));
            }
        }

        return new ProjectEstimate(
            projectId, project.BudgetHours,
            lines.Sum(l => l.EstimatedCost), lines.Sum(l => l.EstimatedRevenue), lines.Sum(l => l.EstimatedProfit),
            lines);
    }
}
