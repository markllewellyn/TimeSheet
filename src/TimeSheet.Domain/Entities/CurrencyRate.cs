namespace TimeSheet.Domain.Entities;

/// <summary>
/// Write-through cache AND audit trail of fetched FX rates. Re-running a historical report months later must
/// reproduce the exact figure originally computed, so reports resolve to the stored rate for a given date,
/// never a live "current" rate.
/// </summary>
public class CurrencyRate
{
    public int Id { get; set; }
    public required string BaseCurrency { get; set; }
    public required string QuoteCurrency { get; set; }
    public DateOnly RateDate { get; set; }

    /// <summary>1 BaseCurrency = Rate QuoteCurrency.</summary>
    public decimal Rate { get; set; }

    public DateTimeOffset FetchedAtUtc { get; set; }
    public string Source { get; set; } = "Frankfurter";
}
