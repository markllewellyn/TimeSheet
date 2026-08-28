using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

/// <summary>
/// Only evaluated on entry CREATION - an entry being newly added is the only case where "existing counted
/// hours" (which naturally excludes the not-yet-saved pending entry) gives a correct before/after comparison
/// without double-counting. Editing an already-saved entry's hours is out of scope for this check for now.
/// </summary>
public class BudgetMonitoringService(IProjectRepository projects, ITimesheetEntryRepository entries, IAppSettingsRepository appSettings) : IBudgetMonitoringService
{
    public async Task<BudgetCheckResult> EvaluateAsync(TimesheetEntry pendingEntry, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(pendingEntry.ProjectId, ct);
        if (project?.BudgetHours is null || project.BudgetHours.Value <= 0)
        {
            return new BudgetCheckResult(RequiresEscalation: false, CumulativeValue: 0, Limit: null, NewlyCrossedNotificationThresholdPercent: null);
        }

        var existingHours = await entries.GetTotalCountedHoursForProjectAsync(pendingEntry.ProjectId, ct);
        var cumulative = existingHours + pendingEntry.WorkHours + pendingEntry.OutOfHoursHours;
        var limit = project.BudgetHours.Value;
        var requiresEscalation = cumulative > limit;

        // FDD: "Project feedback notifications are raised at 50% and 75% of the time allotted to a project...
        // The percentages are held in settings and can be changed by management." Notify once per
        // newly-crossed threshold only (never repeat one already fired) - same "don't repeat an alert that's
        // already fired" principle as ProjectHealthService's notify-on-worsening-only. Flagging (over 100%,
        // above) and this notification are mutually exclusive outcomes - an over-budget entry is flagged
        // instead, per FDD's flagging model, never both.
        int? newlyCrossed = null;
        if (!requiresEscalation)
        {
            var settings = await appSettings.GetAsync(ct);
            var percentConsumed = cumulative / limit * 100m;
            var alreadyNotified = project.HighestBudgetNotificationPercent ?? 0;

            if (percentConsumed >= settings.ProjectBudgetAlertThresholdPercent && alreadyNotified < settings.ProjectBudgetAlertThresholdPercent)
            {
                newlyCrossed = settings.ProjectBudgetAlertThresholdPercent;
            }
            else if (percentConsumed >= settings.ProjectBudgetWarningThresholdPercent && alreadyNotified < settings.ProjectBudgetWarningThresholdPercent)
            {
                newlyCrossed = settings.ProjectBudgetWarningThresholdPercent;
            }
        }

        return new BudgetCheckResult(requiresEscalation, cumulative, limit, newlyCrossed);
    }
}
