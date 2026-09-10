using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(int id, CancellationToken ct);
    Task<Invoice?> GetDraftAsync(int clientId, DateOnly periodStart, CancellationToken ct);
    Task<IReadOnlyList<Invoice>> GetByClientAsync(int clientId, CancellationToken ct);

    /// <summary>Every invoice with at least one line item against any of the given projects, across all
    /// clients - powers a project manager's "invoices for my projects" view (FDD: "Project managers can view
    /// the staged invoice for their projects"), where a PM's projects can span multiple clients.</summary>
    Task<IReadOnlyList<Invoice>> GetByProjectIdsAsync(IReadOnlyCollection<int> projectIds, CancellationToken ct);
    Task<bool> InvoiceNumberInUseAsync(int clientId, string invoiceNumber, CancellationToken ct);
    Task AddAsync(Invoice invoice, CancellationToken ct);
    void Update(Invoice invoice);

    /// <summary>Only ever called on a Draft - InvoicingService.DeleteDraftAsync enforces that. Safe because a
    /// Draft never has any TimesheetEntry/ExpenseEntry pointing an InvoiceId at it (only Finalize sets that),
    /// so there's nothing to unlock first; its InvoiceLineItems cascade-delete automatically.</summary>
    void Remove(Invoice invoice);

    /// <summary>Clears an existing Draft's line items so it can be rebuilt from scratch (e.g. a late timesheet
    /// entry came in) - nothing has gone to the client yet, so a Draft is always safe to regenerate.</summary>
    void ClearLineItems(Invoice invoice);
}
