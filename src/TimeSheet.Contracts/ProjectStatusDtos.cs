namespace TimeSheet.Contracts;

/// <summary>Mirrors TimeSheet.Domain.Services.ProjectStatus - the single-project detail view (see
/// Projects_Status), as opposed to the same figures bundled onto ProjectDto for the list endpoints.</summary>
public record ProjectStatusDto(
    int ProjectId,
    decimal ActualHours, decimal? BudgetHours, decimal? HoursUsedPercent,
    decimal ActualCost, decimal? FixedFeeAmount, decimal? CostUsedPercent);
