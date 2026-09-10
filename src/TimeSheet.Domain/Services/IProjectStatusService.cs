using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Services;

/// <summary>User-requested "is this project on track" view - actual hours logged so far against
/// Project.BudgetHours, and actual cost incurred so far against Project.FixedFeeAmount. Deliberately just these
/// two objective, already-stored figures - no AI/subjective judgment, unlike the removed ProjectHealth feature
/// (see HANDOFF's 2026-09-03 "Project Health removed entirely" entry). Computed on read, same pattern as
/// IProjectEstimateService, but this looks backward at what's already been logged rather than forward at a
/// planned allocation. Takes already-loaded Project entities rather than ids - every call site (Projects_Status,
/// the various Projects_List* endpoints) already has them loaded, so there's no reason for this service to
/// re-fetch and risk an N+1 pattern on the list endpoints.</summary>
public interface IProjectStatusService
{
    Task<ProjectStatus> GetStatusAsync(Project project, CancellationToken ct);

    Task<IReadOnlyDictionary<int, ProjectStatus>> GetStatusesAsync(IReadOnlyCollection<Project> projects, CancellationToken ct);
}

/// <summary>HoursUsedPercent/CostUsedPercent are null when there's nothing to compare against (no BudgetHours /
/// no FixedFeeAmount set) - the UI shows a neutral "no budget set" badge rather than a misleading 0%.</summary>
public record ProjectStatus(
    int ProjectId,
    decimal ActualHours, decimal? BudgetHours, decimal? HoursUsedPercent,
    decimal ActualCost, decimal? FixedFeeAmount, decimal? CostUsedPercent);
