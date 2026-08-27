namespace TimeSheet.Contracts;

public record EntryFlagDto(
    int Id, int TimesheetEntryId, int ProjectId, string? ProjectName, string? ClientName,
    string Reason, decimal BudgetLimitAtTimeOfEntry, decimal CumulativeValueAtTimeOfEntry,
    int? RaisedByUserId, string? RaisedNotes, DateTimeOffset RaisedAtUtc,
    bool IsCleared, int? ClearedByUserId, DateTimeOffset? ClearedAtUtc, string? ClearedNotes);

public record RaiseEntryFlagRequest(int TimesheetEntryId, string? Notes);

public record ClearEntryFlagRequest(string? Notes);
