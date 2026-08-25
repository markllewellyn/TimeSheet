using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

/// <summary>
/// Cache-first (write-through): a resolved rate is persisted under the CALLER'S requested `asOf` date, so a
/// second lookup for that same date is always a cache hit and, once recorded, a historical report/invoice can
/// never have its figures silently change later - even if the live provider's history were to change or
/// become unavailable. If the provider has no rate for `asOf` yet (e.g. requesting "today" before the daily
/// publish), this walks back a few days to find the most recent available rate and caches that value under
/// the originally-requested date.
/// </summary>
public class CurrencyConversionService(ICurrencyRateRepository rates, ICurrencyRateProvider provider, IUnitOfWork uow) : ICurrencyConversionService
{
    private const int MaxLookbackDays = 7;

    public async Task<decimal> GetRateAsync(string fromCurrency, string toCurrency, DateOnly asOf, CancellationToken ct)
    {
        if (fromCurrency == toCurrency) return 1m;

        var cached = await rates.GetAsync(fromCurrency, toCurrency, asOf, ct);
        if (cached is not null) return cached.Rate;

        for (var lookback = 0; lookback <= MaxLookbackDays; lookback++)
        {
            var candidateDate = asOf.AddDays(-lookback);
            var fetched = await provider.FetchRateAsync(fromCurrency, toCurrency, candidateDate, ct);
            if (fetched is null) continue;

            var rate = new CurrencyRate
            {
                BaseCurrency = fromCurrency,
                QuoteCurrency = toCurrency,
                RateDate = asOf,
                Rate = fetched.Value,
                FetchedAtUtc = DateTimeOffset.UtcNow,
                Source = "Frankfurter",
            };
            await rates.AddAsync(rate, ct);
            await uow.SaveChangesAsync(ct);
            return fetched.Value;
        }

        throw new InvalidOperationException($"Could not resolve a currency rate for {fromCurrency}->{toCurrency} as of {asOf:yyyy-MM-dd}.");
    }

    public async Task<Money> ConvertAsync(Money amount, string toCurrency, DateOnly asOf, CancellationToken ct)
    {
        var rate = await GetRateAsync(amount.Currency, toCurrency, asOf, ct);
        return new Money(Math.Round(amount.Amount * rate, 2), toCurrency);
    }

    public async Task<IReadOnlyDictionary<DateOnly, decimal>> GetRatesForRangeAsync(string fromCurrency, string toCurrency, DateOnly start, DateOnly end, CancellationToken ct)
    {
        if (fromCurrency == toCurrency)
        {
            return Enumerable.Range(0, end.DayNumber - start.DayNumber + 1)
                .ToDictionary(offset => start.AddDays(offset), _ => 1m);
        }

        var cached = await rates.GetRangeAsync(fromCurrency, toCurrency, start, end, ct);
        var result = new Dictionary<DateOnly, decimal>();

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            result[date] = cached.TryGetValue(date, out var cachedRate)
                ? cachedRate.Rate
                : await GetRateAsync(fromCurrency, toCurrency, date, ct);
        }

        return result;
    }
}
