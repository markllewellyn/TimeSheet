using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Services;

public record BudgetCheckResult(bool RequiresEscalation, bool IsWarningOnly, decimal CumulativeValue, decimal? Limit);

/// <summary>
/// Detection is synchronous, called from the timesheet-entry creation/update service before final commit - a
/// user must never be able to silently exceed budget. Not a background sweep: the check has to gate the save
/// itself.
/// </summary>
public interface IBudgetMonitoringService
{
    Task<BudgetCheckResult> EvaluateAsync(TimesheetEntry pendingEntry, CancellationToken ct);
}
