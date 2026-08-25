namespace TimeSheet.Domain.Entities;

public class Notification
{
    public int Id { get; set; }
    public int RecipientUserId { get; set; }
    public User? RecipientUser { get; set; }

    public NotificationType Type { get; set; }
    public required string Message { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ReadAtUtc { get; set; }

    public int? RelatedProjectId { get; set; }
    public int? RelatedTimesheetEntryId { get; set; }
}
