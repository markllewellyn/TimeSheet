namespace TimeSheet.Domain.Repositories;

public record ReportScope(int? ClientId, int? ProjectId);

/// <summary>BilledAmountNative/CostAmountNative are summed directly from each entry's stamped
/// ResolvedCustomerRate/ResolvedHourlyCost/ResolvedOutOfHoursCost (see TimesheetEntry) - not re-resolved via
/// IRateResolver at report time, so reports reflect the rate that actually applied when each entry was
/// recorded, never "today's" rate. Entries with no snapshot (pre-migration) contribute 0 to both.
/// BilledAmountNative is additionally forced to 0 for any entry whose project has CanInvoice not true - a
/// project InvoiceGenerationService will never actually invoice must not show as revenue/profit in reports
/// either. CostAmountNative is unaffected by CanInvoice (a non-invoiceable project can still have a real cost
/// impact) - see Project.IsCostExempt for the flag that zeroes cost instead.
/// RoleId/RoleName reflect the staff member's CURRENT User.JobRoleId/Role - not effective-dated, same accepted
/// limitation as RateCard-tier resolution (see Role.cs). RoleName is "Unassigned" when JobRoleId is null.
/// Adding Role to the grouping key alongside User is a no-op on row count/granularity - a user's role is a
/// simple current-state fact of that user, so it can never split an existing (Project,Client,User,Date) group
/// into more than one row. EntryCount is the raw TimesheetEntry row count within this bucket, for reports that
/// want "number of entries" (FDD) rather than just hours/amounts.</summary>
public record TimeEntryAggregateRow(
    int ProjectId, string ProjectName, int ClientId, string ClientName,
    int UserId, string UserName, int? RoleId, string RoleName, DateOnly Date, decimal WorkHours, decimal OutOfHoursHours,
    decimal BilledAmountNative, decimal CostAmountNative, int EntryCount);

public record ExpenseAggregateRow(int ProjectId, int ClientId, DateOnly Date, decimal Amount, string Currency);

/// <summary>
/// One row per (Project, User, Date) - day granularity bounds the aggregate row count by the date range
/// (<=366 rows for the 1-year filter), not by raw timesheet-entry volume, which is what keeps this efficient
/// on SQLite without a materialized rollup table. Every entry counts - an EntryFlag is a prompt to query an
/// entry, not a gate, so a flagged entry is never excluded here (FDD).
/// </summary>
public interface IReportingRepository
{
    Task<IReadOnlyList<TimeEntryAggregateRow>> GetTimeEntryAggregatesAsync(ReportScope scope, DateOnly start, DateOnly end, CancellationToken ct);
    Task<IReadOnlyList<ExpenseAggregateRow>> GetExpenseAggregatesAsync(ReportScope scope, DateOnly start, DateOnly end, CancellationToken ct);
}
