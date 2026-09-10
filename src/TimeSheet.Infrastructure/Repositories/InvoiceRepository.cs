using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class InvoiceRepository(TimesheetDbContext db) : IInvoiceRepository
{
    public Task<Invoice?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Invoices
            .Include(i => i.LineItems).ThenInclude(l => l.Project)
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<Invoice?> GetDraftAsync(int clientId, DateOnly periodStart, CancellationToken ct) =>
        db.Invoices.Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.ClientId == clientId && i.PeriodStart == periodStart && i.Status == InvoiceStatus.Draft, ct);

    public Task<Invoice?> GetOverlappingDraftAsync(int clientId, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct) =>
        db.Invoices.FirstOrDefaultAsync(i =>
            i.ClientId == clientId &&
            i.Status == InvoiceStatus.Draft &&
            i.PeriodStart <= periodEnd &&
            i.PeriodEnd >= periodStart, ct);

    public async Task<IReadOnlyList<Invoice>> GetByClientAsync(int clientId, CancellationToken ct) =>
        await db.Invoices
            .Include(i => i.Client)
            .Include(i => i.LineItems).ThenInclude(l => l.Project)
            .Where(i => i.ClientId == clientId).OrderByDescending(i => i.PeriodStart).ToListAsync(ct);

    public async Task<IReadOnlyList<Invoice>> GetByProjectIdsAsync(IReadOnlyCollection<int> projectIds, CancellationToken ct) =>
        await db.Invoices
            .Include(i => i.Client)
            .Include(i => i.LineItems).ThenInclude(l => l.Project)
            .Where(i => i.LineItems.Any(l => projectIds.Contains(l.ProjectId)))
            .OrderByDescending(i => i.PeriodStart).ToListAsync(ct);

    // Status != Draft (not just == Finalized) - a Voided invoice keeps its InvoiceNumber permanently, and
    // that number must never be reused by a later invoice for the same client. Matches the DB-level partial
    // unique index, which is filtered to the same two statuses for the same reason.
    public Task<bool> InvoiceNumberInUseAsync(int clientId, string invoiceNumber, CancellationToken ct) =>
        db.Invoices.AnyAsync(i => i.ClientId == clientId && i.InvoiceNumber == invoiceNumber && i.Status != InvoiceStatus.Draft, ct);

    public async Task AddAsync(Invoice invoice, CancellationToken ct) => await db.Invoices.AddAsync(invoice, ct);

    public void Update(Invoice invoice) => db.Invoices.Update(invoice);

    public void Remove(Invoice invoice) => db.Invoices.Remove(invoice);

    public void ClearLineItems(Invoice invoice)
    {
        db.InvoiceLineItems.RemoveRange(invoice.LineItems);
        invoice.LineItems.Clear();
    }

    public Task<bool> HasFixedFeeBeenInvoicedAsync(int projectId, CancellationToken ct) =>
        db.InvoiceLineItems.AnyAsync(l =>
            l.ProjectId == projectId &&
            l.Type == InvoiceLineItemType.FixedFee &&
            l.Invoice!.Status == InvoiceStatus.Finalized, ct);
}
