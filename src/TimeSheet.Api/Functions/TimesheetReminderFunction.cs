using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TimeSheet.Domain;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>
/// Daily, weekday-morning sweep: any active User with at least one active Project assignment but no timesheet
/// entry in the last 3 business days gets a reminder. Isolated-worker Timer triggers work identically to
/// in-process (no compatibility concern).
/// </summary>
public class TimesheetReminderFunction(
    IUserRepository users,
    IProjectAssignmentRepository assignments,
    ITimesheetEntryRepository entries,
    INotificationService notificationService,
    ILogger<TimesheetReminderFunction> logger)
{
    [Function("DailyTimesheetReminder")]
    public async Task Run([TimerTrigger("0 0 7 * * 1-5")] TimerInfo timer, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
        var lookbackStart = today.AddDays(-5); // covers the last ~3 business days across a weekend

        var activeUsers = await users.GetAllAsync(includeInactive: false, ct);
        var remindersSent = 0;

        foreach (var user in activeUsers)
        {
            var activeAssignments = await assignments.GetByUserIdAsync(user.Id, activeOnly: true, ct);
            if (activeAssignments.Count == 0) continue;

            var recentEntries = await entries.GetForUserAsync(user.Id, searchText: null, from: lookbackStart, to: today, ct);
            if (recentEntries.Count > 0) continue;

            await notificationService.RaiseAsync(
                user.Id,
                NotificationType.TimesheetReminder,
                "You haven't logged any timesheet entries recently - please update your timesheet.",
                NotificationChannel.Both,
                ct: ct);
            remindersSent++;
        }

        logger.LogInformation("Daily timesheet reminder sweep complete: {Count} reminders sent.", remindersSent);
    }
}
