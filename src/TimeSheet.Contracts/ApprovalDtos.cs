namespace TimeSheet.Contracts;

/// <summary>A timesheet entry as seen on the Admin Approvals screen - the payroll/approval workflow's own
/// shape, distinct from TimesheetEntryDto (which is the caller's-own-timesheet view).</summary>
public record ApprovalEntryDto(
    int Id, int StaffId, string StaffName, DateOnly Date, string Description,
    int ProjectId, string ProjectName, int ClientId, string ClientName,
    decimal WorkHours, decimal OutOfHoursHours, decimal ToPayroll,
    bool ApprovedPayroll, bool SentToPayroll, string? PostingBatch);

public record PendingApprovalsResponse(IReadOnlyList<ApprovalEntryDto> Entries, IReadOnlyList<string> PostingBatches);

public record ApproveEntriesRequest(int[] EntryIds, string PostingBatch);

public record SendToPayrollRequest(int[] EntryIds);
