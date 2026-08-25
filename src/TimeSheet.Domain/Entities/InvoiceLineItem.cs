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

    /// <summary>Frozen, in Invoice.ReportingCurrency.</summary>
    public decimal Amount { get; set; }

    public InvoiceLineItemType Type { get; set; }
}
