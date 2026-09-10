namespace TimeSheet.Contracts;

/// <summary>Mirrors TimeSheet.Domain.Services.ProjectBreakdown - the project detail drill-down page's "who has
/// done what, and on what" view (by staff, by entry type, by month), as opposed to the aggregate figures on
/// ProjectStatusDto.</summary>
public record ProjectBreakdownDto(
    int ProjectId,
    IReadOnlyList<StaffBreakdownLineDto> ByStaff,
    IReadOnlyList<EntryTypeBreakdownLineDto> ByEntryType,
    IReadOnlyList<MonthBreakdownLineDto> ByMonth,
    decimal? RecognizedRevenueToDate);

public record StaffBreakdownLineDto(int UserId, string UserName, decimal Hours, decimal Cost, decimal Revenue, decimal Profit);

public record EntryTypeBreakdownLineDto(int? EntryTypeId, string EntryTypeName, decimal Hours);

public record MonthBreakdownLineDto(int Year, int Month, decimal Hours);
