using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Services;

public interface IEntryFlagService
{
    /// <summary>System-raised - a project budget/allocation was exceeded. Never blocks the entry; it saves and
    /// counts normally immediately (see BudgetMonitoringService/TimesheetEntriesFunctions.Create).</summary>
    Task<EntryFlag> RaiseSystemAsync(TimesheetEntry entry, EntryFlagReason reason, decimal budgetLimit, decimal cumulativeValue, CancellationToken ct);

    /// <summary>An admin/PM flagging an entry for query without a system-triggering condition.</summary>
    Task<EntryFlag> RaiseManualAsync(int timesheetEntryId, int raisedByUserId, string? notes, CancellationToken ct);

    /// <summary>The entry has been queried and settled - no separate approve/decline outcome, just cleared.</summary>
    Task<EntryFlag> ClearAsync(int flagId, int clearedByUserId, string? notes, CancellationToken ct);

    /// <summary>A distinct, explicit action from raising - lets an admin/PM prompt the staff member about a
    /// flagged entry rather than that happening automatically on raise.</summary>
    Task NotifyStaffAsync(int flagId, CancellationToken ct);
}
