namespace TimeSheet.Domain.Entities;

public class InvoiceLineItem
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    /// <summary>FDD: a finalized invoice line shows "the staff member's name, the project name, the task date,
    /// the description... " - one InvoiceLineItem per TimesheetEntry/ExpenseEntry (see InvoiceGenerationService),
    /// so this is that entry's own description, not a project-level rollup label.</summary>
    public required string Description { get; set; }

    /// <summary>Null for Fixed Fee lines - no natural per-entry shape.</summary>
    public decimal? Hours { get; set; }

    /// <summary>Snapshot of who logged the underlying entry - FK id + a denormalized name, same pattern as
    /// TimesheetEntry.ApprovedByStaffId/ApprovedByName, so an invoice stays reproducible even if the person is
    /// later renamed or deactivated. Null for Fixed Fee lines.</summary>
    public int? StaffId { get; set; }
    public string? StaffName { get; set; }

    /// <summary>The underlying TimesheetEntry/ExpenseEntry's own Date. Null for Fixed Fee lines.</summary>
    public DateOnly? TaskDate { get; set; }

    /// <summary>The rate actually billed for this line - TimesheetEntry.ResolvedCustomerRate, snapshotted
    /// as-is (native currency, not converted) so the invoice can always be reproduced from its own data, same
    /// principle as the resolved rate already stored on the entry itself. Null for Fixed Fee and Expense lines
    /// (an expense has no per-hour rate).</summary>
    public decimal? Rate { get; set; }

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
