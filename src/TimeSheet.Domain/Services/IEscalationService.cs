using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Services;

public interface IEscalationService
{
    Task<Escalation> RaiseAsync(TimesheetEntry entry, EscalationReason reason, decimal budgetLimit, decimal cumulativeValue, CancellationToken ct);

    /// <summary>Approve is an explicit one-off exception for this occurrence only - Project.BudgetHours is never
    /// silently raised as a side effect, so the next entry that again exceeds the (still-original) budget
    /// triggers its own new escalation.</summary>
    Task<Escalation> ApproveAsync(int escalationId, int adminUserId, string? notes, CancellationToken ct);

    /// <summary>Decline excludes the entry from billing/reporting; the submitting user is notified and must
    /// revise (edit hours/project/date) and resubmit.</summary>
    Task<Escalation> DeclineAsync(int escalationId, int adminUserId, string? notes, CancellationToken ct);
}
