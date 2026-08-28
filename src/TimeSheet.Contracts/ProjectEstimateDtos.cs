namespace TimeSheet.Contracts;

public record ProjectEstimateLineDto(
    int UserId, string UserName, int? RoleId, string? RoleName, decimal AllocatedHours,
    decimal? HourlyCost, decimal? CustomerRate, decimal EstimatedCost, decimal EstimatedRevenue, decimal EstimatedProfit,
    string? Warning);

public record ProjectEstimateDto(
    int ProjectId, decimal? BudgetHours, decimal EstimatedCost, decimal EstimatedRevenue, decimal EstimatedProfit,
    IReadOnlyList<ProjectEstimateLineDto> Lines);
