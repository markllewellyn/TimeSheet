using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class EscalationService(
    IEscalationRepository escalations,
    ITimesheetEntryRepository entries,
    INotificationService notificationService,
    IUnitOfWork uow) : IEscalationService
{
    public async Task<Escalation> RaiseAsync(TimesheetEntry entry, EscalationReason reason, decimal budgetLimit, decimal cumulativeValue, CancellationToken ct)
    {
        var escalation = new Escalation
        {
            TimesheetEntryId = entry.Id,
            ProjectId = entry.ProjectId,
            Reason = reason,
            BudgetLimitAtTimeOfEntry = budgetLimit,
            CumulativeValueAtTimeOfEntry = cumulativeValue,
            RaisedAtUtc = DateTimeOffset.UtcNow,
            Decision = EscalationDecision.Pending,
        };
        await escalations.AddAsync(escalation, ct);
        await uow.SaveChangesAsync(ct);

        // Timeliness matters most here - Both channels, unlike the softer BudgetWarning case.
        await notificationService.RaiseToAdminsAsync(
            NotificationType.EscalationRaised,
            $"A timesheet entry on project {entry.ProjectId} exceeds its budget and needs your approval.",
            NotificationChannel.Both,
            relatedProjectId: entry.ProjectId,
            relatedTimesheetEntryId: entry.Id,
            ct: ct);

        return escalation;
    }

    public async Task<Escalation> ApproveAsync(int escalationId, int adminUserId, string? notes, CancellationToken ct)
    {
        var escalation = await escalations.GetByIdAsync(escalationId, ct)
            ?? throw new InvalidOperationException($"Escalation {escalationId} not found.");

        escalation.Decision = EscalationDecision.Approved;
        escalation.DecidedByUserId = adminUserId;
        escalation.DecidedAtUtc = DateTimeOffset.UtcNow;
        escalation.DecisionNotes = notes;
        escalations.Update(escalation);

        // An explicit one-off exception for THIS occurrence only - Project.BudgetHours is never silently
        // raised, so the next over-budget entry raises its own new escalation.
        var entry = await entries.GetByIdAsync(escalation.TimesheetEntryId, ct);
        if (entry is not null)
        {
            entry.Status = TimesheetEntryStatus.Approved;
            entries.Update(entry);
        }

        await uow.SaveChangesAsync(ct);

        if (entry is not null)
        {
            await notificationService.RaiseAsync(
                entry.UserId, NotificationType.EscalationDecided,
                "Your over-budget timesheet entry was approved.",
                NotificationChannel.InAppOnly, entry.ProjectId, entry.Id, ct);
        }

        return escalation;
    }

    public async Task<Escalation> DeclineAsync(int escalationId, int adminUserId, string? notes, CancellationToken ct)
    {
        var escalation = await escalations.GetByIdAsync(escalationId, ct)
            ?? throw new InvalidOperationException($"Escalation {escalationId} not found.");

        escalation.Decision = EscalationDecision.Declined;
        escalation.DecidedByUserId = adminUserId;
        escalation.DecidedAtUtc = DateTimeOffset.UtcNow;
        escalation.DecisionNotes = notes;
        escalations.Update(escalation);

        var entry = await entries.GetByIdAsync(escalation.TimesheetEntryId, ct);
        if (entry is not null)
        {
            entry.Status = TimesheetEntryStatus.Declined;
            entries.Update(entry);
        }

        await uow.SaveChangesAsync(ct);

        if (entry is not null)
        {
            await notificationService.RaiseAsync(
                entry.UserId, NotificationType.EscalationDecided,
                "Your over-budget timesheet entry was declined - please revise it and resubmit.",
                NotificationChannel.Both, entry.ProjectId, entry.Id, ct);
        }

        return escalation;
    }
}
