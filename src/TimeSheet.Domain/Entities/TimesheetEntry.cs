namespace TimeSheet.Domain.Entities;

public class TimesheetEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public DateOnly Date { get; set; }
    public decimal WorkHours { get; set; }
    public decimal OutOfHoursHours { get; set; }
    public string? Description { get; set; }

    /// <summary>Normal entries count toward billing/reporting immediately. PendingApproval entries are excluded
    /// until an Admin decides (see Escalation). Declined entries are excluded permanently.</summary>
    public TimesheetEntryStatus Status { get; set; } = TimesheetEntryStatus.Normal;

    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? ModifiedUtc { get; set; }

    public List<Attachment> Attachments { get; set; } = [];
}
