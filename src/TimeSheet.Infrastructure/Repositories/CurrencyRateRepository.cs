using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class CurrencyRateRepository(TimesheetDbContext db) : ICurrencyRateRepository
{
    public Task<CurrencyRate?> GetAsync(string baseCurrency, string quoteCurrency, DateOnly date, CancellationToken ct) =>
        db.CurrencyRates.FirstOrDefaultAsync(r =>
            r.BaseCurrency == baseCurrency && r.QuoteCurrency == quoteCurrency && r.RateDate == date, ct);

    public async Task<IReadOnlyDictionary<DateOnly, CurrencyRate>> GetRangeAsync(string baseCurrency, string quoteCurrency, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var rates = await db.CurrencyRates
            .Where(r => r.BaseCurrency == baseCurrency && r.QuoteCurrency == quoteCurrency && r.RateDate >= start && r.RateDate <= end)
            .ToListAsync(ct);
        return rates.ToDictionary(r => r.RateDate);
    }

    public async Task AddAsync(CurrencyRate rate, CancellationToken ct) => await db.CurrencyRates.AddAsync(rate, ct);
}
