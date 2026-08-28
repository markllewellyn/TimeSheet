namespace TimeSheet.Domain.Services;

/// <summary>FDD: "A project holds an estimated cost and an estimated profit, calculated per user and per role
/// from the assigned staff, their costs and the applicable rate cards." Computed on read, not persisted -
/// composes IStaffProjectRepository + IRateResolver fresh (see ProjectEstimateService), the same pattern
/// already used by UserWorkloadService/ProjectAssignmentsFunctions.Create for a rate-resolution preview.
/// Project.BudgetHours is split across active assignments weighted by their AllocatedHoursPerWeek (an equal
/// split when nobody has one set) - there is no separate per-assignment "allotted hours" input.</summary>
public interface IProjectEstimateService
{
    Task<ProjectEstimate> EstimateAsync(int projectId, CancellationToken ct);
}

public record ProjectEstimate(
    int ProjectId, decimal? BudgetHours, decimal EstimatedCost, decimal EstimatedRevenue, decimal EstimatedProfit,
    IReadOnlyList<ProjectEstimateLine> Lines);

/// <summary>Warning is set (and EstimatedCost/EstimatedRevenue/EstimatedProfit left at 0) when this assignee has
/// no resolvable rate/cost yet (RateNotConfiguredException) - surfaced per-line rather than failing the whole
/// estimate, since one incomplete assignment shouldn't hide the rest.</summary>
public record ProjectEstimateLine(
    int UserId, string UserName, int? RoleId, string? RoleName, decimal AllocatedHours,
    decimal? HourlyCost, decimal? CustomerRate, decimal EstimatedCost, decimal EstimatedRevenue, decimal EstimatedProfit,
    string? Warning);
