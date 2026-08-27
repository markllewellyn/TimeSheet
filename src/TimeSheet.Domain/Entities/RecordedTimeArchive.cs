namespace TimeSheet.Domain.Entities;

/// <summary>
/// Maps onto the legacy [RecordedTimesarc] table - an archive of old RecordedTimes rows, moved here with their
/// original Id preserved (non-identity PK). Present for schema fidelity only; nothing in the app currently
/// writes to this table - archiving a payroll period is a future feature.
/// </summary>
public class RecordedTimeArchive
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ClientId { get; set; }
    public int ProjectId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateOnly Date { get; set; }
    public required string Description { get; set; }
    public decimal ToPayroll { get; set; }
    public decimal ToCompany { get; set; }
    public decimal WorkHours { get; set; }
    public decimal OutOfHoursHours { get; set; }
    public bool ApprovedPayroll { get; set; }
    public int? ApprovedByStaffId { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTimeOffset? DateApprovedPayroll { get; set; }
    public bool SentToPayroll { get; set; }
    public int? SentByStaffId { get; set; }
    public string? SentByName { get; set; }
    public DateTimeOffset? DateSentToPayroll { get; set; }
    public string? PostingBatch { get; set; }
    public decimal? ExpensesValue { get; set; }
}
