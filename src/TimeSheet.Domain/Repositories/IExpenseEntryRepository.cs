using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IExpenseEntryRepository
{
    Task<ExpenseEntry?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<ExpenseEntry>> GetForUserAsync(int userId, string? searchText, DateOnly? from, DateOnly? to, CancellationToken ct);

    /// <summary>Billable expenses for a project within a date range, across all users - used by invoicing.
    /// Excludes any expense already locked to a prior invoice (InvoiceId is not null), so a second draft for an
    /// overlapping period never double-counts an already-invoiced expense.</summary>
    Task<IReadOnlyList<ExpenseEntry>> GetBillableForProjectAsync(int projectId, DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>Every expense currently locked to this invoice - used by InvoicingService.VoidInvoiceAsync to
    /// unlock them all (InvoiceId = null) so they become invoiceable again.</summary>
    Task<IReadOnlyList<ExpenseEntry>> GetByInvoiceIdAsync(int invoiceId, CancellationToken ct);

    Task AddAsync(ExpenseEntry entry, CancellationToken ct);
    void Update(ExpenseEntry entry);
    void Remove(ExpenseEntry entry);
}
