using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class EntryFlagService(
    IEntryFlagRepository flags,
    ITimesheetEntryRepository entries,
    INotificationService notificationService,
    IUnitOfWork uow) : IEntryFlagService
{
    public async Task<EntryFlag> RaiseSystemAsync(TimesheetEntry entry, EntryFlagReason reason, decimal budgetLimit, decimal cumulativeValue, CancellationToken ct)
    {
        var flag = new EntryFlag
        {
            TimesheetEntryId = entry.Id,
            ProjectId = entry.ProjectId,
            Reason = reason,
            BudgetLimitAtTimeOfEntry = budgetLimit,
            CumulativeValueAtTimeOfEntry = cumulativeValue,
            RaisedAtUtc = DateTimeOffset.UtcNow,
        };
        await flags.AddAsync(flag, ct);
        await uow.SaveChangesAsync(ct);

        // Timeliness matters most here - Both channels. The entry itself is never blocked; this is purely
        // "please go query this."
        await notificationService.RaiseToAdminsAsync(
            NotificationType.FlagRaised,
            $"A timesheet entry on project {entry.ProjectId} exceeds its budget and has been flagged for query.",
            NotificationChannel.Both,
            relatedProjectId: entry.ProjectId,
            relatedTimesheetEntryId: entry.Id,
            ct: ct);

        return flag;
    }

    public async Task<EntryFlag> RaiseManualAsync(int timesheetEntryId, int raisedByUserId, string? notes, CancellationToken ct)
    {
        var entry = await entries.GetByIdAsync(timesheetEntryId, ct)
            ?? throw new InvalidOperationException($"Timesheet entry {timesheetEntryId} not found.");

        var flag = new EntryFlag
        {
            TimesheetEntryId = entry.Id,
            ProjectId = entry.ProjectId,
            Reason = EntryFlagReason.Manual,
            RaisedByUserId = raisedByUserId,
            RaisedNotes = notes,
            RaisedAtUtc = DateTimeOffset.UtcNow,
        };
        await flags.AddAsync(flag, ct);
        await uow.SaveChangesAsync(ct);

        return flag;
    }

    public async Task<EntryFlag> ClearAsync(int flagId, int clearedByUserId, string? notes, CancellationToken ct)
    {
        var flag = await flags.GetByIdAsync(flagId, ct)
            ?? throw new InvalidOperationException($"Entry flag {flagId} not found.");

        flag.IsCleared = true;
        flag.ClearedByUserId = clearedByUserId;
        flag.ClearedAtUtc = DateTimeOffset.UtcNow;
        flag.ClearedNotes = notes;
        flags.Update(flag);
        await uow.SaveChangesAsync(ct);

        return flag;
    }

    public async Task NotifyStaffAsync(int flagId, CancellationToken ct)
    {
        var flag = await flags.GetByIdAsync(flagId, ct)
            ?? throw new InvalidOperationException($"Entry flag {flagId} not found.");
        var entry = flag.TimesheetEntry ?? await entries.GetByIdAsync(flag.TimesheetEntryId, ct)
            ?? throw new InvalidOperationException($"Timesheet entry {flag.TimesheetEntryId} not found.");

        await notificationService.RaiseAsync(
            entry.UserId, NotificationType.FlagRaised,
            "One of your timesheet entries has been flagged for query - please check it.",
            NotificationChannel.Both, entry.ProjectId, entry.Id, ct);
    }
}
