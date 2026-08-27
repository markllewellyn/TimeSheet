namespace TimeSheet.Contracts;

public record ExpenseEntryDto(
    int Id, int ProjectId, string ProjectName, int ClientId, string ClientName,
    DateOnly Date, decimal Amount, string Currency, string? Description, bool IsBillable, string Kind);

/// <summary>Kind defaults to "Expense" when omitted. "Contract" is Admin-only and restricted to projects where
/// CanInvoice is not true - see ExpenseEntriesFunctions.Create.</summary>
public record CreateExpenseEntryRequest(int ProjectId, DateOnly Date, decimal Amount, string Currency, string? Description, bool IsBillable, string? Kind = null);

public record UpdateExpenseEntryRequest(DateOnly Date, decimal Amount, string Currency, string? Description, bool IsBillable);
