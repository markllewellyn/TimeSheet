using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

/// <summary>
/// Only evaluated on entry CREATION - an entry being newly added is the only case where "existing counted
/// hours" (which naturally excludes the not-yet-saved pending entry) gives a correct before/after comparison
/// without double-counting. Editing an already-saved entry's hours is out of scope for this check for now.
/// </summary>
public class BudgetMonitoringService(IProjectRepository projects, ITimesheetEntryRepository entries) : IBudgetMonitoringService
{
    public async Task<BudgetCheckResult> EvaluateAsync(TimesheetEntry pendingEntry, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(pendingEntry.ProjectId, ct);
        if (project?.BudgetHours is null)
        {
            return new BudgetCheckResult(RequiresEscalation: false, IsWarningOnly: false, CumulativeValue: 0, Limit: null);
        }

        var existingHours = await entries.GetTotalCountedHoursForProjectAsync(pendingEntry.ProjectId, ct);
        var cumulative = existingHours + pendingEntry.WorkHours + pendingEntry.OutOfHoursHours;
        var limit = project.BudgetHours.Value;
        var warningThreshold = limit * project.BudgetAlertThresholdPercent / 100m;

        var requiresEscalation = cumulative > limit;
        var isWarningOnly = !requiresEscalation && cumulative >= warningThreshold;

        return new BudgetCheckResult(requiresEscalation, isWarningOnly, cumulative, limit);
    }
}
