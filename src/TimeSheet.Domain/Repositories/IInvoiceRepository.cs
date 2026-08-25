using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(int id, CancellationToken ct);
    Task<Invoice?> GetDraftAsync(int clientId, DateOnly periodStart, CancellationToken ct);
    Task<IReadOnlyList<Invoice>> GetByClientAsync(int clientId, CancellationToken ct);
    Task<bool> InvoiceNumberInUseAsync(int clientId, string invoiceNumber, CancellationToken ct);
    Task AddAsync(Invoice invoice, CancellationToken ct);
    void Update(Invoice invoice);

    /// <summary>Clears an existing Draft's line items so it can be rebuilt from scratch (e.g. a late timesheet
    /// entry came in) - nothing has gone to the client yet, so a Draft is always safe to regenerate.</summary>
    void ClearLineItems(Invoice invoice);
}
