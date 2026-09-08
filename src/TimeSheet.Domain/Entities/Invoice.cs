namespace TimeSheet.Domain.Entities;

public class Invoice
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public Client? Client { get; set; }

    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }

    /// <summary>Frozen copy of the client's reporting currency at generation time — the currency every line amount is converted into.</summary>
    public required string ReportingCurrency { get; set; }

    /// <summary>The manually-entered "1 GBP = X ReportingCurrency" rate applied at generation time, replacing
    /// automatic conversion for every line on this invoice, when one was supplied. Null for a GBP client or when
    /// generated without a manual rate. Persisted for audit and printed on the finalized PDF.</summary>
    public decimal? ExchangeRate { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    /// <summary>Null while Draft. Required, manually entered by an Admin, to Finalize.</summary>
    public string? InvoiceNumber { get; set; }

    public decimal TotalAmount { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public DateTimeOffset? FinalizedAtUtc { get; set; }
    public int? FinalizedByUserId { get; set; }

    /// <summary>Opaque key into IFileStorageService. Only populated at Finalize time (so the invoice number
    /// appears on the rendered document).</summary>
    public string? PdfStorageKey { get; set; }

    public List<InvoiceLineItem> LineItems { get; set; } = [];
}
