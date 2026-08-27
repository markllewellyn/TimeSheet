using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface ITimesheetEntryRepository
{
    Task<TimesheetEntry?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>The signed-in user's own entries (a User can only ever see/edit their own timesheet), optionally
    /// filtered by a free-text search over customer/project name and a date range - mirrors the legacy "Log
    /// Time" grid's search box.</summary>
    Task<IReadOnlyList<TimesheetEntry>> GetForUserAsync(int userId, string? searchText, DateOnly? from, DateOnly? to, CancellationToken ct);

    /// <summary>Admin-wide - every entry across every staff member within the date range (open-ended on either
    /// side when null). Powers the master-list CSV export.</summary>
    Task<IReadOnlyList<TimesheetEntry>> GetAllInRangeAsync(DateOnly? from, DateOnly? to, CancellationToken ct);

    /// <summary>All Normal/Approved entries for a project within a date range, across all users - used by
    /// invoicing (billing is per-project, not per-user) and, later, reporting.</summary>
    Task<IReadOnlyList<TimesheetEntry>> GetCountedForProjectAsync(int projectId, DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>Sum of WorkHours+OutOfHoursHours for a project across entries that count toward its budget
    /// (Normal + Approved - PendingApproval/Declined are excluded until/unless approved). Used by
    /// IBudgetMonitoringService to evaluate a new entry against Project.BudgetHours.</summary>
    Task<decimal> GetTotalCountedHoursForProjectAsync(int projectId, CancellationToken ct);

    /// <summary>Sum of WorkHours+OutOfHoursHours for ALL of a user's entries on a date, regardless of Status
    /// (Declined/PendingApproval count too) - a physical "a day only has 24 hours" cap, not a payroll rule.
    /// excludeEntryId lets Update exclude the row being edited (its OLD hours are still persisted at validation time).</summary>
    Task<decimal> GetTotalHoursForUserDateAsync(int userId, DateOnly date, int? excludeEntryId, CancellationToken ct);

    /// <summary>Entries not yet approved for payroll, across all users - the Admin Approvals queue. Filtered by
    /// a free-text search over staff/customer/project name, matching the legacy Approval tab's search box.</summary>
    Task<IReadOnlyList<TimesheetEntry>> GetPendingApprovalAsync(string? searchText, CancellationToken ct);

    /// <summary>Approved but not yet sent to payroll - the Admin "Ready for Payroll" queue.</summary>
    Task<IReadOnlyList<TimesheetEntry>> GetReadyForPayrollAsync(CancellationToken ct);

    /// <summary>Distinct non-empty PostingBatch values ever used, for the "existing batch" picker on approve.</summary>
    Task<IReadOnlyList<string>> GetDistinctPostingBatchesAsync(CancellationToken ct);

    /// <summary>Bulk fetch for the Approve / Send-to-Payroll actions, which mutate many entries in one call.</summary>
    Task<IReadOnlyList<TimesheetEntry>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct);

    Task AddAsync(TimesheetEntry entry, CancellationToken ct);
    void Update(TimesheetEntry entry);
    void Remove(TimesheetEntry entry);
}
