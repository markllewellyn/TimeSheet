namespace TimeSheet.Domain.Repositories;

public record ReportScope(int? ClientId, int? ProjectId);

public record TimeEntryAggregateRow(
    int ProjectId, string ProjectName, int ClientId, string ClientName,
    int UserId, string UserName, DateOnly Date, decimal WorkHours, decimal OutOfHoursHours);

public record ExpenseAggregateRow(int ProjectId, int ClientId, DateOnly Date, decimal Amount, string Currency);

/// <summary>
/// One row per (Project, User, Date) - day granularity bounds the aggregate row count by the date range
/// (<=366 rows for the 1-year filter), not by raw timesheet-entry volume, which is what keeps this efficient
/// on SQLite without a materialized rollup table. Only Normal/Approved entries count (PendingApproval/Declined
/// are excluded), matching the same "counted" definition used by budget monitoring and invoicing.
/// </summary>
public interface IReportingRepository
{
    Task<IReadOnlyList<TimeEntryAggregateRow>> GetTimeEntryAggregatesAsync(ReportScope scope, DateOnly start, DateOnly end, CancellationToken ct);
    Task<IReadOnlyList<ExpenseAggregateRow>> GetExpenseAggregatesAsync(ReportScope scope, DateOnly start, DateOnly end, CancellationToken ct);
}
