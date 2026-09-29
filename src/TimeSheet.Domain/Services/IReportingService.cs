namespace TimeSheet.Domain.Services;

public enum ReportRangePreset { Last7Days, LastMonth, LastYear, Custom }

public record ReportDateRange(DateOnly Start, DateOnly End, ReportRangePreset Preset);

public record ReportEnvelope<TSummary, TLine>(ReportDateRange Range, string Currency, TSummary Summary, IReadOnlyList<TLine> Breakdown);

// EntryCount (raw TimesheetEntry row count, not day-count or hour-sum) is on every summary/line - FDD:
// "the number of entries... on a team, role and user basis." Team is a plain admin-managed staff grouping (see
// Team.cs) - a second, independent reporting axis alongside Role and User, with no billing significance of its
// own (see IReportingService's own doc comment below for the "OnProjectByTeam" reports this powers).
//
// BudgetHours/HoursRemaining (FDD's "hours remaining" ask, previously only visible via the separate Budget &
// Cost Status feature - see IProjectStatusService) are ALL-TIME figures against Project.BudgetHours, exactly
// like IProjectStatusService's own ActualHours/BudgetHours - NOT scoped to the report's own date Range, since
// a budget isn't a per-period concept in this app (see HANDOFF's still-open "should BudgetHours reset per
// billing period" ambiguity). Both are null when the project has no BudgetHours set, same "no misleading 0%"
// convention as ProjectStatus. Only ever populated on a genuinely project-scoped summary/line - see each
// record's own site below for which.
public record TimeSummary(decimal WorkHours, decimal OutOfHoursHours, decimal TotalHours, int EntryCount, decimal? BudgetHours = null, decimal? HoursRemaining = null);
public record TimeByUserLine(int UserId, string UserName, decimal WorkHours, decimal OutOfHoursHours, decimal TotalHours, int EntryCount);
/// <summary>BudgetHours/HoursRemaining here are this specific project's own all-time figures (see the doc
/// comment above) - populated on every line, since each line already is one project. Deliberately NOT also
/// summed onto GetTimeOnClientReportAsync's own TimeSummary: a client's "hours remaining" isn't one honest
/// number when its projects have different (or no) budgets, so that summary leaves both fields null rather
/// than publish a misleading aggregate - the per-project truth lives here instead.</summary>
public record TimeByProjectLine(int ProjectId, string ProjectName, decimal WorkHours, decimal OutOfHoursHours, decimal TotalHours, int EntryCount, decimal? BudgetHours = null, decimal? HoursRemaining = null);
public record TimeByRoleLine(int? RoleId, string RoleName, decimal WorkHours, decimal OutOfHoursHours, decimal TotalHours, int EntryCount);
public record TimeByTeamLine(int? TeamId, string TeamName, decimal WorkHours, decimal OutOfHoursHours, decimal TotalHours, int EntryCount);

public record CostSummary(decimal LaborCost, decimal ExpenseCost, decimal TotalCost, int EntryCount);
public record CostByUserLine(int UserId, string UserName, decimal LaborCost, decimal ExpenseCost, decimal TotalCost, int EntryCount);
public record CostByProjectLine(int ProjectId, string ProjectName, decimal LaborCost, decimal ExpenseCost, decimal TotalCost, int EntryCount);
/// <summary>ExpenseCost is always 0 here - expenses (ExpenseEntry) have no staff/role dimension at all, only a
/// project, so there's no way to attribute one to a specific role. Matches CostByUserLine's existing precedent
/// of the same limitation (also always 0 there, for the same reason).</summary>
public record CostByRoleLine(int? RoleId, string RoleName, decimal LaborCost, decimal ExpenseCost, decimal TotalCost, int EntryCount);
/// <summary>Same ExpenseCost-always-0 limitation as CostByRoleLine, for the same reason (expenses have no
/// team dimension either).</summary>
public record CostByTeamLine(int? TeamId, string TeamName, decimal LaborCost, decimal ExpenseCost, decimal TotalCost, int EntryCount);

public record ProfitSummary(decimal Billed, decimal Cost, decimal Profit, decimal MarginPercent, int EntryCount);
public record ProfitByUserLine(int UserId, string UserName, decimal Billed, decimal Cost, decimal Profit, int EntryCount);
public record ProfitByProjectLine(int ProjectId, string ProjectName, decimal Billed, decimal Cost, decimal Profit, decimal MarginPercent, int EntryCount);
public record ProfitByRoleLine(int? RoleId, string RoleName, decimal Billed, decimal Cost, decimal Profit, int EntryCount);
public record ProfitByTeamLine(int? TeamId, string TeamName, decimal Billed, decimal Cost, decimal Profit, int EntryCount);

/// <summary>
/// One strongly-typed method per report (not a fully generic dispatcher - the six reports genuinely differ in
/// shape/metric) built on the shared IReportingRepository aggregation core, so the underlying EF queries
/// aren't duplicated six times. "On Client" reports break down by Project; "On Project" reports break down by
/// User - never three levels nested in one payload.
///
/// The 3 "OnProjectByRole" and 3 "OnProjectByTeam" methods are FDD's "role... [and] team... basis" ask - a
/// Role-grouped or Team-grouped alternate view of the same per-project data the User-basis reports already show
/// (RoleName/TeamName "Unassigned" for a staff member with no JobRoleId/TeamId). Deliberately NOT added for "on
/// Client" too, for either dimension: a client's report already spans multiple projects that can mix Time&
/// Materials and Fixed Fee payment models, and Fixed Fee revenue is recognized at the whole-project level
/// (IRevenueRecognitionService) - there's no honest way to slice that recognized revenue down to "this role's
/// [or team's] share of it", so a Client-scoped Profit/Cost-by-Role/Team would either misrepresent recognized
/// revenue or need a materially bigger design (Time-by-Role/Team-on-Client alone would be safe as hours have no
/// such attribution problem, but was left out too for consistency - all three metrics or none, per scope). Flag
/// for a human call if Client-scoped role/team reporting turns out to be wanted after all.
///
/// Neither Role nor Team is effective-dated (see Role.cs/Team.cs) - a role/team-basis report reflects each
/// staff member's CURRENT role/team, not whatever they held on the entry's own date. This is an accepted,
/// already-existing limitation (RateCard-tier resolution has exactly the same "role today assumed = role back
/// then" caveat for Role), not a new one introduced for Team.
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

    Task<ReportEnvelope<TimeSummary, TimeByTeamLine>> GetTimeOnProjectByTeamReportAsync(int projectId, ReportDateRange range, CancellationToken ct);
    Task<ReportEnvelope<CostSummary, CostByTeamLine>> GetCostOnProjectByTeamReportAsync(int projectId, ReportDateRange range, CancellationToken ct);
    Task<ReportEnvelope<ProfitSummary, ProfitByTeamLine>> GetProfitOnProjectByTeamReportAsync(int projectId, ReportDateRange range, CancellationToken ct);
}

/// <summary>
/// Isolates Fixed-Project-Cost revenue recognition (hours consumed / BudgetHours * FixedFeeAmount) from the
/// trivial Time & Materials case (hours * billing rate), so the two payment models don't tangle in the same
/// code path.
/// </summary>
public interface IRevenueRecognitionService
{
    /// <summary>Revenue recognized for <paramref name="hoursInPeriod"/>, capped so a project never recognizes more
    /// than its FixedFeeAmount in total: only hours up to BudgetHours count, measured cumulatively from the
    /// project's start - so <paramref name="hoursBeforePeriod"/> (hours logged before the period) is needed to know
    /// how much budget was already used. Pass 0 for an all-time figure.</summary>
    Task<decimal> GetRecognizedRevenueAsync(int projectId, decimal hoursBeforePeriod, decimal hoursInPeriod, string targetCurrency, DateOnly asOf, CancellationToken ct);
}
