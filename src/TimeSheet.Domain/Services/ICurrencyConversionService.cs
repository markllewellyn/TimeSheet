namespace TimeSheet.Domain.Services;

public record Money(decimal Amount, string Currency);

/// <summary>
/// Amounts are stored in their native/original currency at the point of entry; conversion happens only at
/// display/report/invoice time via this service - never baked into stored data (see the plan's Currency
/// conversion section). The one deliberate exception is a Finalized Invoice, which freezes its converted
/// amounts permanently.
/// </summary>
public interface ICurrencyConversionService
{
    Task<decimal> GetRateAsync(string fromCurrency, string toCurrency, DateOnly asOf, CancellationToken ct);

    Task<Money> ConvertAsync(Money amount, string toCurrency, DateOnly asOf, CancellationToken ct);

    /// <summary>Bulk range fetch so reports doing money math across many dates cost one round trip, not one
    /// call per underlying entry.</summary>
    Task<IReadOnlyDictionary<DateOnly, decimal>> GetRatesForRangeAsync(string fromCurrency, string toCurrency, DateOnly start, DateOnly end, CancellationToken ct);
}

/// <summary>The swappable external-provider boundary (Infrastructure implements this against Frankfurter today).</summary>
public interface ICurrencyRateProvider
{
    /// <summary>Returns null if the provider has no rate for this exact date (e.g. requested before today's
    /// daily rate has published) - the caller falls back to the most recent available date.</summary>
    Task<decimal?> FetchRateAsync(string baseCurrency, string quoteCurrency, DateOnly date, CancellationToken ct);
}
