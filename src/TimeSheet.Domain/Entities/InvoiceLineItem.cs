namespace TimeSheet.Domain.Entities;

public class InvoiceLineItem
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    /// <summary>Presentation-cleaned text — internal-only fields (raw TimesheetEntry.Description, attachments,
    /// escalation history, UserId internals) are never surfaced here by default.</summary>
    public required string Description { get; set; }

    /// <summary>Null for Fixed Fee lines.</summary>
    public decimal? Hours { get; set; }

    /// <summary>The amount InvoiceGenerationService computed, before any staging-time discount - frozen the
    /// same way Amount is, in Invoice.ReportingCurrency. Equal to Amount until a discount is applied.</summary>
    public decimal GrossAmount { get; set; }

    /// <summary>FDD: "A discount can be applied... against an individual invoice line while the invoice is
    /// being staged." 0-100, null = no discount. Only settable while the invoice is Draft - see
    /// IInvoicingService.ApplyLineItemDiscountAsync. Wiped if the Draft is regenerated (GenerateDraftInvoiceAsync
    /// clears and rebuilds LineItems from scratch) - a known, acceptable limitation, not a bug.</summary>
    public decimal? DiscountPercent { get; set; }

    /// <summary>Frozen, in Invoice.ReportingCurrency. Equal to GrossAmount, less DiscountPercent if one is set.</summary>
    public decimal Amount { get; set; }

    public InvoiceLineItemType Type { get; set; }
}
