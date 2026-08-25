namespace TimeSheet.Domain.Services;

public enum ReportRangePreset { Last7Days, LastMonth, LastYear, Custom }

public record ReportDateRange(DateOnly Start, DateOnly End, ReportRangePreset Preset);

public record ReportEnvelope<TSummary, TLine>(ReportDateRange Range, string Currency, TSummary Summary, IReadOnlyList<TLine> Breakdown);

public record TimeSummary(decimal WorkHours, decimal OutOfHoursHours, decimal TotalHours);
public record TimeByUserLine(int UserId, string UserName, decimal WorkHours, decimal OutOfHoursHours, decimal TotalHours);
public record TimeByProjectLine(int ProjectId, string ProjectName, decimal WorkHours, decimal OutOfHoursHours, decimal TotalHours);

public record CostSummary(decimal LaborCost, decimal ExpenseCost, decimal TotalCost);
public record CostByUserLine(int UserId, string UserName, decimal LaborCost, decimal ExpenseCost, decimal TotalCost);
public record CostByProjectLine(int ProjectId, string ProjectName, decimal LaborCost, decimal ExpenseCost, decimal TotalCost);

public record ProfitSummary(decimal Billed, decimal Cost, decimal Profit, decimal MarginPercent);
public record ProfitByUserLine(int UserId, string UserName, decimal Billed, decimal Cost, decimal Profit);
public record ProfitByProjectLine(int ProjectId, string ProjectName, decimal Billed, decimal Cost, decimal Profit, decimal MarginPercent);

/// <summary>
/// One strongly-typed method per report (not a fully generic dispatcher - the six reports genuinely differ in
/// shape/metric) built on the shared IReportingRepository aggregation core, so the underlying EF queries
/// aren't duplicated six times. "On Client" reports break down by Project; "On Project" reports break down by
/// User - never three levels nested in one payload.
/// </summary>
public interface IReportingService
{
    Task<ReportEnvelope<TimeSummary, TimeByUserLine>> GetTimeOnProjectReportAsync(int projectId, ReportDateRange range, CancellationToken ct);
    Task<ReportEnvelope<TimeSummary, TimeByProjectLine>> GetTimeOnClientReportAsync(int clientId, ReportDateRange range, CancellationToken ct);
    Task<ReportEnvelope<CostSummary, CostByUserLine>> GetCostOnProjectReportAsync(int projectId, ReportDateRange range, CancellationToken ct);
    Task<ReportEnvelope<CostSummary, CostByProjectLine>> GetCostOnClientReportAsync(int clientId, ReportDateRange range, CancellationToken ct);
    Task<ReportEnvelope<ProfitSummary, ProfitByUserLine>> GetProfitOnProjectReportAsync(int projectId, ReportDateRange range, CancellationToken ct);
    Task<ReportEnvelope<ProfitSummary, ProfitByProjectLine>> GetProfitOnClientReportAsync(int clientId, ReportDateRange range, CancellationToken ct);
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
