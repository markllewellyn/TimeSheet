namespace TimeSheet.Contracts;

public record ProjectDto(
    int Id, int ClientId, string ClientName, string Name, string Code, string? Description,
    string PaymentModel, string? CurrencyOverride, DateOnly StartDate, DateOnly? EndDate,
    decimal? BudgetHours, decimal? FixedFeeAmount, int BudgetAlertThresholdPercent, bool IsActive);

/// <summary>DefaultCostRatePerHour/DefaultBillingRatePerHour seed the project's first default (project-wide)
/// ProjectRate atomically with the project itself.</summary>
public record CreateProjectRequest(
    int ClientId, string Name, string Code, string? Description,
    string PaymentModel, string? CurrencyOverride, DateOnly StartDate, DateOnly? EndDate,
    decimal? BudgetHours, decimal? FixedFeeAmount, int BudgetAlertThresholdPercent,
    decimal DefaultCostRatePerHour, decimal? DefaultBillingRatePerHour);

public record UpdateProjectRequest(
    string Name, string? Description, string? CurrencyOverride, DateOnly? EndDate,
    decimal? BudgetHours, decimal? FixedFeeAmount, int BudgetAlertThresholdPercent, bool IsActive);

public record ProjectRateDto(int Id, int ProjectId, int? UserId, string? UserName, decimal? BillingRatePerHour, decimal CostRatePerHour, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public record UpsertProjectRateRequest(int? UserId, decimal? BillingRatePerHour, decimal CostRatePerHour, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
