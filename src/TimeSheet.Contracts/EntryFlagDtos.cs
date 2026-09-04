namespace TimeSheet.Contracts;

public record EntryFlagDto(
    int Id, int TimesheetEntryId, int ProjectId, string? ProjectName, string? ClientName,
    string Reason, decimal BudgetLimitAtTimeOfEntry, decimal CumulativeValueAtTimeOfEntry,
    int? RaisedByUserId, string? RaisedNotes, DateTimeOffset RaisedAtUtc,
    bool IsCleared, int? ClearedByUserId, DateTimeOffset? ClearedAtUtc, string? ClearedNotes);

public record RaiseEntryFlagRequest(int TimesheetEntryId, string? Notes);

public record ClearEntryFlagRequest(string? Notes);

/// <summary>A timesheet entry as shown in the "raise a flag" picker's search results - lets an admin/PM find an
/// entry to flag without needing to already know its raw numeric id.</summary>
public record EntryFlagSearchResultDto(
    int Id, int StaffId, string StaffName, DateOnly Date, string Description,
    string ProjectName, string ClientName, decimal WorkHours, decimal OutOfHoursHours);
