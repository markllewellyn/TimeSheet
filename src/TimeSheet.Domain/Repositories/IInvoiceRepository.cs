using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(int id, CancellationToken ct);
    Task<Invoice?> GetDraftAsync(int clientId, DateOnly periodStart, CancellationToken ct);

    /// <summary>The first other Draft invoice for this client whose period overlaps [periodStart, periodEnd] -
    /// used to block generating a second, genuinely different draft while one already exists for an overlapping
    /// span. Deliberately Draft-only: a Finalized or Voided invoice covering an overlapping period is fine and
    /// must NOT block a new draft - GetCountedForInvoicingAsync/GetBillableForProjectAsync/
    /// HasFixedFeeBeenInvoicedAsync already correctly exclude whatever that invoice locked or billed, so the new
    /// draft just comes back with less on it, not blocked outright. Only a still-open Draft is the real risk: it
    /// was never locked, so leaving it around unrefreshed while another invoice for an overlapping period gets
    /// finalized is exactly how a client ends up billed twice for the same work.</summary>
    Task<Invoice?> GetOverlappingDraftAsync(int clientId, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct);

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

    /// <summary>Whether this project's Fixed Fee has already been billed on a Finalized invoice - a Fixed
    /// Project Cost project's flat fee is a one-off charge (FDD: "a project is either a fixed one-off piece of
    /// time or repeating time on a monthly basis"), unlike Time &amp; Materials entries and expenses, which bill
    /// per period. Only Finalized counts, matching the same "Draft locks nothing yet" convention already used
    /// for TimesheetEntry/ExpenseEntry (so regenerating the very Draft that already carries this project's fee
    /// doesn't strip it back out); a Voided invoice is deliberately excluded so voiding correctly makes the fee
    /// billable again.</summary>
    Task<bool> HasFixedFeeBeenInvoicedAsync(int projectId, CancellationToken ct);
}
