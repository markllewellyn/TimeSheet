using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class CurrencyRepository(TimesheetDbContext db) : ICurrencyRepository
{
    public Task<Currency?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Currencies.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Currency>> GetAllAsync(CancellationToken ct) =>
        await db.Currencies.OrderBy(c => c.CurrencyCode).ToListAsync(ct);
}
