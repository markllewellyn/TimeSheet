namespace TimeSheet.Domain.Entities;

public class ExpenseEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    /// <summary>Expense (default, any assigned staff member) vs Contract (Admin-only, logged against a
    /// non-invoiceable project) - see ExpenseEntriesFunctions.Create for the validation split.</summary>
    public ExpenseEntryKind Kind { get; set; } = ExpenseEntryKind.Expense;

    public DateOnly Date { get; set; }

    /// <summary>Stored in the currency it was actually incurred in — never converted at write time (see ICurrencyConversionService).</summary>
    public decimal Amount { get; set; }
    public required string Currency { get; set; }

    public string? Description { get; set; }

    /// <summary>Whether this expense is rebilled to the client (vs. absorbed as internal cost) — read by Cost/Profit reports and invoicing.</summary>
    public bool IsBillable { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? ModifiedUtc { get; set; }

    public List<ExpenseAttachment> Attachments { get; set; } = [];
}
