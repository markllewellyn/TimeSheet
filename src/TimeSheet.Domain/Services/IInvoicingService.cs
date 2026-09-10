using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Services;

/// <summary>Pure collation/calculation - grouping, rounding, hiding internal-only fields, currency conversion,
/// rate/fixed-fee application. No persistence; IInvoicingService owns the Draft/Finalize lifecycle.</summary>
public interface IInvoiceGenerationService
{
    Task<Invoice> BuildDraftAsync(int clientId, DateOnly periodStart, DateOnly periodEnd, decimal? manualExchangeRate, CancellationToken ct);
}

public interface IInvoicingService
{
    Task<Invoice> GenerateDraftInvoiceAsync(int clientId, DateOnly periodStart, DateOnly periodEnd, decimal? manualExchangeRate, CancellationToken ct);

    /// <summary>FDD: "A discount can be applied... against an individual invoice line while the invoice is
    /// being staged." Only while the invoice is Draft - throws otherwise. Recomputes the line's Amount from its
    /// frozen GrossAmount and the invoice's TotalAmount from all lines. Passing null clears any discount.</summary>
    Task<Invoice> ApplyLineItemDiscountAsync(int invoiceId, int lineItemId, decimal? discountPercent, CancellationToken ct);

    /// <summary>Bulk sibling of ApplyLineItemDiscountAsync - applies the same discount to every line on this
    /// invoice for one project, in one call, so an admin doesn't have to discount a project's dozens of
    /// per-entry lines one at a time. Same Draft-only/0-100 rules; passing null clears the discount on every
    /// matching line.</summary>
    Task<Invoice> ApplyProjectDiscountAsync(int invoiceId, int projectId, decimal? discountPercent, CancellationToken ct);

    /// <summary>Requires a non-empty, unique (per client) invoice number. Renders the PDF at this point (so the
    /// invoice number appears on the document) and freezes the record - no further edits possible after.</summary>
    Task<Invoice> FinalizeInvoiceAsync(int invoiceId, string invoiceNumber, int finalizedByUserId, CancellationToken ct);

    Task<Stream> GetPdfAsync(int invoiceId, CancellationToken ct);

    /// <summary>Nothing was ever locked for a Draft, so this is a plain delete - no entries to unwind. Throws
    /// if the invoice isn't a Draft.</summary>
    Task DeleteDraftAsync(int invoiceId, CancellationToken ct);

    /// <summary>Keeps the Invoice row, its real InvoiceNumber and its PDF permanently (a durable record this
    /// number was issued then voided) - unlocks every entry/expense that was locked to it so they become
    /// invoiceable again. Cannot be called on a Draft (nothing to void) or an already-Voided invoice.</summary>
    Task<Invoice> VoidInvoiceAsync(int invoiceId, string? reason, int voidedByUserId, string voidedByName, CancellationToken ct);
}

public record InvoiceDocumentModel(
    string ClientName,
    string? ClientAddress,
    string InvoiceNumber,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string Currency,
    decimal? ExchangeRate,
    IReadOnlyList<InvoiceDocumentLine> LineItems,
    decimal TotalAmount);

public record InvoiceDocumentLine(
    string ProjectName, string? StaffName, DateOnly? TaskDate, string Description, decimal? Hours, decimal? Rate, decimal Amount);

/// <summary>The renderer never touches EF entities directly - only this plain DTO - keeping the PDF library
/// (QuestPDF today) fully swappable.</summary>
public interface IPdfInvoiceRenderer
{
    Task<byte[]> RenderAsync(InvoiceDocumentModel model, CancellationToken ct);
}
