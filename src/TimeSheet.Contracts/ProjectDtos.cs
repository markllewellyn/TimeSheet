namespace TimeSheet.Contracts;

public record ProjectDto(
    int Id, int ClientId, string ClientName, string Name, string Code, string? Description,
    string PaymentModel, string ProjectType, bool? CanInvoice, string? CurrencyOverride, DateOnly StartDate, DateOnly? EndDate,
    decimal? BudgetHours, decimal? FixedFeeAmount, bool IsActive,
    int? ProjectManagerUserId, string? ProjectManagerName);

public record CreateProjectRequest(
    int ClientId, string Name, string Code, string? Description,
    string PaymentModel, string ProjectType, bool? CanInvoice, string? CurrencyOverride, DateOnly StartDate, DateOnly? EndDate,
    decimal? BudgetHours, decimal? FixedFeeAmount, int? ProjectManagerUserId);

public record UpdateProjectRequest(
    string Name, string? Description, string ProjectType, bool? CanInvoice, string? CurrencyOverride, DateOnly? EndDate,
    decimal? BudgetHours, decimal? FixedFeeAmount, bool IsActive, int? ProjectManagerUserId);
