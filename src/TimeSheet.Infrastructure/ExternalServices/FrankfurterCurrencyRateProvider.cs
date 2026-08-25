using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.ExternalServices;

/// <summary>
/// Frankfurter (api.frankfurter.dev) - free, no API key, built on ECB daily reference rates. Chosen over
/// exchangerate-api.com/Open Exchange Rates for zero signup friction and official, reproducible daily rates
/// (good semantics for "the rate on date X", which matters for historical report/invoice stability).
/// </summary>
public class FrankfurterCurrencyRateProvider(HttpClient httpClient, ILogger<FrankfurterCurrencyRateProvider> logger) : ICurrencyRateProvider
{
    public async Task<decimal?> FetchRateAsync(string baseCurrency, string quoteCurrency, DateOnly date, CancellationToken ct)
    {
        if (baseCurrency == quoteCurrency) return 1m;

        var dateSegment = date.ToString("yyyy-MM-dd");
        var url = $"v1/{dateSegment}?base={baseCurrency}&symbols={quoteCurrency}";

        try
        {
            using var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Frankfurter returned {StatusCode} for {Url}", response.StatusCode, url);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("rates", out var rates) ||
                !rates.TryGetProperty(quoteCurrency, out var rateElement))
            {
                return null;
            }

            return rateElement.GetDecimal();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Failed to fetch currency rate {Base}->{Quote} for {Date}", baseCurrency, quoteCurrency, date);
            return null;
        }
    }
}
