using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Services;

/// <summary>User-requested "who has done what, and on what" drill-down for a project's actual logged time to
/// date - a companion to IProjectStatusService's aggregate on-track figures. Three independent breakdowns of
/// the same underlying entries: by staff member (with cost/revenue/profit), by entry type/category, and by
/// calendar month.</summary>
public interface IProjectBreakdownService
{
    Task<ProjectBreakdown> GetBreakdownAsync(Project project, CancellationToken ct);
}

public record ProjectBreakdown(
    int ProjectId,
    IReadOnlyList<StaffBreakdownLine> ByStaff,
    IReadOnlyList<EntryTypeBreakdownLine> ByEntryType,
    IReadOnlyList<MonthBreakdownLine> ByMonth,
    // Only non-null for a FixedProjectCost project - revenue is recognized at the whole-project level there
    // (see ProjectBreakdownService), not attributable per person, matching
    // ReportingService.GetProfitOnProjectReportAsync's own identical rule.
    decimal? RecognizedRevenueToDate);

public record StaffBreakdownLine(int UserId, string UserName, decimal Hours, decimal Cost, decimal Revenue, decimal Profit);

public record EntryTypeBreakdownLine(int? EntryTypeId, string EntryTypeName, decimal Hours);

public record MonthBreakdownLine(int Year, int Month, decimal Hours);
