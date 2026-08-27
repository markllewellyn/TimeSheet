namespace TimeSheet.Domain.Entities;

/// <summary>
/// A record of who changed what and when, including who was impersonating at the time (FDD Key Entities).
/// Id-and-snapshot only, deliberately with no FK/cascade relations - the audit trail must survive the thing it
/// describes (a user, an entry) being deleted later. See IAuditLogService for how this is written.
/// </summary>
public class AuditLog
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public required string UserDisplayName { get; set; }

    /// <summary>Set only when the action was performed by an Admin actively impersonating this user (FDD:
    /// "actions performed while impersonating are written to the audit log against both the administrator and
    /// the impersonated user").</summary>
    public int? ImpersonatedUserId { get; set; }
    public string? ImpersonatedUserDisplayName { get; set; }

    /// <summary>E.g. "TimesheetEntry.Updated".</summary>
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public int? EntityId { get; set; }
    public string? Details { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
}
