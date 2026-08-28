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

    /// <summary>Requires a non-empty, unique (per client) invoice number. Renders the PDF at this point (so the
    /// invoice number appears on the document) and freezes the record - no further edits possible after.</summary>
    Task<Invoice> FinalizeInvoiceAsync(int invoiceId, string invoiceNumber, int finalizedByUserId, CancellationToken ct);

    Task<byte[]> GetPdfAsync(int invoiceId, CancellationToken ct);
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

public record InvoiceDocumentLine(string Description, decimal? Hours, decimal Amount);

/// <summary>The renderer never touches EF entities directly - only this plain DTO - keeping the PDF library
/// (QuestPDF today) fully swappable.</summary>
public interface IPdfInvoiceRenderer
{
    Task<byte[]> RenderAsync(InvoiceDocumentModel model, CancellationToken ct);
}
