namespace TimeSheet.Domain.Entities;

/// <summary>
/// Audit trail for a budget-overrun approval decision. Approving is an explicit one-off exception for this
/// occurrence only — Project.BudgetHours/BudgetAmount is never silently raised as a side effect.
/// </summary>
public class Escalation
{
    public int Id { get; set; }
    public int TimesheetEntryId { get; set; }
    public TimesheetEntry? TimesheetEntry { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public EscalationReason Reason { get; set; }
    public decimal BudgetLimitAtTimeOfEntry { get; set; }
    public decimal CumulativeValueAtTimeOfEntry { get; set; }
    public DateTimeOffset RaisedAtUtc { get; set; }

    public EscalationDecision Decision { get; set; } = EscalationDecision.Pending;
    public int? DecidedByUserId { get; set; }
    public DateTimeOffset? DecidedAtUtc { get; set; }
    public string? DecisionNotes { get; set; }
}
