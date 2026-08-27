namespace TimeSheet.Domain.Entities;

/// <summary>
/// Maps onto the legacy [Currency] table - a master list with a single "current" exchange rate per currency,
/// distinct from the existing CurrencyRate table (a Frankfurter-backed daily-rate cache used for historical FX
/// lookups). The two are bridged only via CurrencyCode; there's no functional wiring between them yet.
/// </summary>
public class Currency
{
    public int Id { get; set; }
    public required string CurrencyName { get; set; }
    public required string CurrencyCode { get; set; }
    public decimal? ExchangeRate { get; set; }
    public bool? IsActive { get; set; } = true;
    public DateTimeOffset? LastUpdated { get; set; }
}
