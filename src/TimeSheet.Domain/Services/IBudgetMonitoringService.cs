using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Services;

/// <summary>NewlyCrossedNotificationThresholdPercent is set only when this entry pushes cumulative consumption
/// past a firm-wide threshold (AppSettings.ProjectBudgetWarningThresholdPercent/AlertThresholdPercent) that
/// hadn't already been notified-for on this project - null on every subsequent entry once notified, and null
/// entirely once RequiresEscalation (the entry is flagged instead - flagging and notification are mutually
/// exclusive outcomes of the same entry).</summary>
public record BudgetCheckResult(bool RequiresEscalation, decimal CumulativeValue, decimal? Limit, int? NewlyCrossedNotificationThresholdPercent);

/// <summary>
/// Detection is synchronous, called from the timesheet-entry creation/update service before final commit - a
/// user must never be able to silently exceed budget. Not a background sweep: the check has to gate the save
/// itself.
/// </summary>
public interface IBudgetMonitoringService
{
    Task<BudgetCheckResult> EvaluateAsync(TimesheetEntry pendingEntry, CancellationToken ct);
}
