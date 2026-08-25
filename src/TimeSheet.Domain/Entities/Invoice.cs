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

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    /// <summary>Null while Draft. Required, manually entered by an Admin, to Finalize.</summary>
    public string? InvoiceNumber { get; set; }

    public decimal TotalAmount { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public DateTimeOffset? FinalizedAtUtc { get; set; }
    public int? FinalizedByUserId { get; set; }

    /// <summary>Only populated at Finalize time (so the invoice number appears on the rendered document).</summary>
    public byte[]? PdfContent { get; set; }

    public List<InvoiceLineItem> LineItems { get; set; } = [];
}
