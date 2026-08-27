namespace TimeSheet.Contracts;

public record ProjectDto(
    int Id, int ClientId, string ClientName, string Name, string Code, string? Description,
    string PaymentModel, bool? CanInvoice, string? CurrencyOverride, DateOnly StartDate, DateOnly? EndDate,
    decimal? BudgetHours, decimal? FixedFeeAmount, int BudgetAlertThresholdPercent, bool IsActive);

public record CreateProjectRequest(
    int ClientId, string Name, string Code, string? Description,
    string PaymentModel, bool? CanInvoice, string? CurrencyOverride, DateOnly StartDate, DateOnly? EndDate,
    decimal? BudgetHours, decimal? FixedFeeAmount, int BudgetAlertThresholdPercent);

public record UpdateProjectRequest(
    string Name, string? Description, bool? CanInvoice, string? CurrencyOverride, DateOnly? EndDate,
    decimal? BudgetHours, decimal? FixedFeeAmount, int BudgetAlertThresholdPercent, bool IsActive);
