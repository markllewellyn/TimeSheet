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

    public async Task<IReadOnlyList<Invoice>> GetByClientAsync(int clientId, CancellationToken ct) =>
        await db.Invoices
            .Include(i => i.Client)
            .Include(i => i.LineItems).ThenInclude(l => l.Project)
            .Where(i => i.ClientId == clientId).OrderByDescending(i => i.PeriodStart).ToListAsync(ct);

    public Task<bool> InvoiceNumberInUseAsync(int clientId, string invoiceNumber, CancellationToken ct) =>
        db.Invoices.AnyAsync(i => i.ClientId == clientId && i.InvoiceNumber == invoiceNumber && i.Status == InvoiceStatus.Finalized, ct);

    public async Task AddAsync(Invoice invoice, CancellationToken ct) => await db.Invoices.AddAsync(invoice, ct);

    public void Update(Invoice invoice) => db.Invoices.Update(invoice);

    public void ClearLineItems(Invoice invoice)
    {
        db.InvoiceLineItems.RemoveRange(invoice.LineItems);
        invoice.LineItems.Clear();
    }
}
