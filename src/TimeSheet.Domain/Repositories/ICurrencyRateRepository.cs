using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface ICurrencyRateRepository
{
    Task<CurrencyRate?> GetAsync(string baseCurrency, string quoteCurrency, DateOnly date, CancellationToken ct);
    Task<IReadOnlyDictionary<DateOnly, CurrencyRate>> GetRangeAsync(string baseCurrency, string quoteCurrency, DateOnly start, DateOnly end, CancellationToken ct);
    Task AddAsync(CurrencyRate rate, CancellationToken ct);
}
