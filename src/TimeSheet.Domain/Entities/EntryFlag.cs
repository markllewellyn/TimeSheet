namespace TimeSheet.Domain.Entities;

/// <summary>
/// A prompt to query an entry, never a gate (FDD: "the entry is saved, remains editable by the staff member,
/// and is carried through to invoicing in the normal way unless someone changes it"). Raised either
/// system-side (RaisedByUserId null - a project budget/allocation was exceeded) or manually by an admin/PM
/// (RaisedByUserId set). Cleared once queried and settled - no separate approve/decline outcome.
/// </summary>
public class EntryFlag
{
    public int Id { get; set; }
    public int TimesheetEntryId { get; set; }
    public TimesheetEntry? TimesheetEntry { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public EntryFlagReason Reason { get; set; }

    /// <summary>Only meaningful for a system-raised budget flag; 0 for Manual.</summary>
    public decimal BudgetLimitAtTimeOfEntry { get; set; }
    public decimal CumulativeValueAtTimeOfEntry { get; set; }

    /// <summary>Null when system-raised.</summary>
    public int? RaisedByUserId { get; set; }
    public DateTimeOffset RaisedAtUtc { get; set; }

    /// <summary>Only meaningful for a manually-raised flag - why the admin/PM is querying this entry.</summary>
    public string? RaisedNotes { get; set; }

    public bool IsCleared { get; set; }
    public int? ClearedByUserId { get; set; }
    public DateTimeOffset? ClearedAtUtc { get; set; }
    public string? ClearedNotes { get; set; }
}
