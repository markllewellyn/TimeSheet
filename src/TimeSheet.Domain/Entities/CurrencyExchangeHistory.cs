namespace TimeSheet.Domain.Entities;

/// <summary>Maps onto the legacy [CurrencyExchangeHistory] table - an audit log of Currency.ExchangeRate changes.</summary>
public class CurrencyExchangeHistory
{
    public int Id { get; set; }
    public int CurrencyId { get; set; }
    public Currency? Currency { get; set; }

    public decimal OldExchangeRate { get; set; }
    public decimal NewExchangeRate { get; set; }
    public DateTimeOffset ChangeDate { get; set; }
}
