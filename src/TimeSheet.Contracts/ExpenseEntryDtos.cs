namespace TimeSheet.Contracts;

public record ExpenseEntryDto(
    int Id, int ProjectId, string ProjectName, int ClientId, string ClientName,
    DateOnly Date, decimal Amount, string Currency, string? Description, bool IsBillable);

public record CreateExpenseEntryRequest(int ProjectId, DateOnly Date, decimal Amount, string Currency, string? Description, bool IsBillable);

public record UpdateExpenseEntryRequest(DateOnly Date, decimal Amount, string Currency, string? Description, bool IsBillable);
