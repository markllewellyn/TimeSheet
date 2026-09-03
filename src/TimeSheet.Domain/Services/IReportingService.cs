namespace TimeSheet.Domain.Services;

public enum ReportRangePreset { Last7Days, LastMonth, LastYear, Custom }

public record ReportDateRange(DateOnly Start, DateOnly End, ReportRangePreset Preset);

public record ReportEnvelope<TSummary, TLine>(ReportDateRange Range, string Currency, TSummary Summary, IReadOnlyList<TLine> Breakdown);

// EntryCount (raw TimesheetEntry row count, not day-count or hour-sum) is on every summary/line - FDD:
// "the number of entries... on a team, role and user basis." ("Team" has no entity in this app's model and
// isn't addressed here - see IReportingService's own doc comment below.)
public record TimeSummary(decimal WorkHours, decimal OutOfHoursHours, decimal TotalHours, int EntryCount);
public record TimeByUserLine(int UserId, string UserName, decimal WorkHours, decimal OutOfHoursHours, decimal TotalHours, int EntryCount);
public record TimeByProjectLine(int ProjectId, string ProjectName, decimal WorkHours, decimal OutOfHoursHours, decimal TotalHours, int EntryCount);
public record TimeByRoleLine(int? RoleId, string RoleName, decimal WorkHours, decimal OutOfHoursHours, decimal TotalHours, int EntryCount);

public record CostSummary(decimal LaborCost, decimal ExpenseCost, decimal TotalCost, int EntryCount);
public record CostByUserLine(int UserId, string UserName, decimal LaborCost, decimal ExpenseCost, decimal TotalCost, int EntryCount);
public record CostByProjectLine(int ProjectId, string ProjectName, decimal LaborCost, decimal ExpenseCost, decimal TotalCost, int EntryCount);
/// <summary>ExpenseCost is always 0 here - expenses (ExpenseEntry) have no staff/role dimension at all, only a
/// project, so there's no way to attribute one to a specific role. Matches CostByUserLine's existing precedent
/// of the same limitation (also always 0 there, for the same reason).</summary>
public record CostByRoleLine(int? RoleId, string RoleName, decimal LaborCost, decimal ExpenseCost, decimal TotalCost, int EntryCount);

public record ProfitSummary(decimal Billed, decimal Cost, decimal Profit, decimal MarginPercent, int EntryCount);
public record ProfitByUserLine(int UserId, string UserName, decimal Billed, decimal Cost, decimal Profit, int EntryCount);
public record ProfitByProjectLine(int ProjectId, string ProjectName, decimal Billed, decimal Cost, decimal Profit, decimal MarginPercent, int EntryCount);
public record ProfitByRoleLine(int? RoleId, string RoleName, decimal Billed, decimal Cost, decimal Profit, int EntryCount);

/// <summary>
/// One strongly-typed method per report (not a fully generic dispatcher - the six reports genuinely differ in
/// shape/metric) built on the shared IReportingRepository aggregation core, so the underlying EF queries
/// aren't duplicated six times. "On Client" reports break down by Project; "On Project" reports break down by
/// User - never three levels nested in one payload.
///
/// The 3 "OnProjectByRole" methods are FDD's "role... basis" ask - a Role-grouped alternate view of the same
/// per-project data the User-basis reports already show (RoleName "Unassigned" for a staff member with no
/// JobRoleId). Deliberately NOT added for "on Client" too: a client's report already spans multiple projects
/// that can mix Time&Materials and Fixed Fee payment models, and Fixed Fee revenue is recognized at the whole-
/// project level (IRevenueRecognitionService) - there's no honest way to slice that recognized revenue down
/// to "this role's share of it", so a Client-scoped Profit/Cost-by-Role would either misrepresent recognized
/// revenue or need a materially bigger design (Time-by-Role-on-Client alone would be safe as hours have no such
/// attribution problem, but was left out too for consistency - all three metrics or none, per scope). Flag for
/// a human call if Client-scoped role reporting turns out to be wanted after all.
///
/// Role itself is NOT effective-dated (see Role.cs) - a role-basis report reflects each staff member's CURRENT
/// role, not whatever role they held on the entry's own date. This is an accepted, already-existing limitation
/// (RateCard-tier resolution has exactly the same "role today assumed = role back then" caveat), not a new one
/// introduced here.
/// </summary>
public interface IReportingService
{
    Task<ReportEnvelope<TimeSummary, TimeByUserLine>> GetTimeOnProjectReportAsync(int projectId, ReportDateRange range, CancellationToken ct);
    Task<ReportEnvelope<TimeSummary, TimeByProjectLine>> GetTimeOnClientReportAsync(int clientId, ReportDateRange range, CancellationToken ct);
    Task<ReportEnvelope<CostSummary, CostByUserLine>> GetCostOnProjectReportAsync(int projectId, ReportDateRange range, CancellationToken ct);
    Task<ReportEnvelope<CostSummary, CostByProjectLine>> GetCostOnClientReportAsync(int clientId, ReportDateRange range, CancellationToken ct);
    Task<ReportEnvelope<ProfitSummary, ProfitByUserLine>> GetProfitOnProjectReportAsync(int projectId, ReportDateRange range, CancellationToken ct);
    Task<ReportEnvelope<ProfitSummary, ProfitByProjectLine>> GetProfitOnClientReportAsync(int clientId, ReportDateRange range, CancellationToken ct);

    Task<ReportEnvelope<TimeSummary, TimeByRoleLine>> GetTimeOnProjectByRoleReportAsync(int projectId, ReportDateRange range, CancellationToken ct);
    Task<ReportEnvelope<CostSummary, CostByRoleLine>> GetCostOnProjectByRoleReportAsync(int projectId, ReportDateRange range, CancellationToken ct);
    Task<ReportEnvelope<ProfitSummary, ProfitByRoleLine>> GetProfitOnProjectByRoleReportAsync(int projectId, ReportDateRange range, CancellationToken ct);
}

/// <summary>
/// Isolates Fixed-Project-Cost revenue recognition (hours consumed / BudgetHours * FixedFeeAmount) from the
/// trivial Time & Materials case (hours * billing rate), so the two payment models don't tangle in the same
/// code path.
/// </summary>
public interface IRevenueRecognitionService
{
    Task<decimal> GetRecognizedRevenueAsync(int projectId, decimal hoursInPeriod, string targetCurrency, DateOnly asOf, CancellationToken ct);
}
