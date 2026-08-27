using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class ClientRepository(TimesheetDbContext db) : IClientRepository
{
    // Always .Include(c => c.Currency) - Client.ReportingCurrencyCode is computed from that navigation and
    // silently falls back to "GBP" if it isn't loaded, which would be a silent money-math bug everywhere a
    // Client is read for reporting/invoicing.
    public Task<Client?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Clients.Include(c => c.AccountManagers).Include(c => c.Currency).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Client>> GetAllAsync(bool includeInactive, CancellationToken ct)
    {
        var query = db.Clients.Include(c => c.Currency).AsQueryable();
        if (!includeInactive) query = query.Where(c => c.IsActive);
        return await query.OrderBy(c => c.Name).ToListAsync(ct);
    }

    public Task<bool> AccountCodeExistsAsync(string accountCode, int? excludeId, CancellationToken ct) =>
        db.Clients.AnyAsync(c => c.AccountCode == accountCode && (excludeId == null || c.Id != excludeId), ct);

    public async Task AddAsync(Client client, CancellationToken ct) => await db.Clients.AddAsync(client, ct);

    public void Update(Client client) => db.Clients.Update(client);
}
