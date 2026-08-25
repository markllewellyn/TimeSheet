namespace TimeSheet.Domain.Entities;

/// <summary>Singleton settings row (single record, Id = 1). Seeds new Clients and covers firm-wide (unscoped) reports.</summary>
public class AppSettings
{
    public int Id { get; set; } = 1;

    /// <summary>Used to normalize firm-wide reports that have no single client/project currency to fall back to.</summary>
    public required string BaseReportingCurrency { get; set; }

    public int DefaultInvoiceMonthEndDay { get; set; } = 31;
}
