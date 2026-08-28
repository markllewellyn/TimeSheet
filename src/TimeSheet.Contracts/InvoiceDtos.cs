namespace TimeSheet.Contracts;

public record InvoiceLineItemDto(int Id, int ProjectId, string ProjectName, string Description, decimal? Hours, decimal GrossAmount, decimal? DiscountPercent, decimal Amount, string Type);

public record InvoiceDto(
    int Id, int ClientId, string ClientName, DateOnly PeriodStart, DateOnly PeriodEnd,
    string ReportingCurrency, decimal? ExchangeRate, string Status, string? InvoiceNumber, decimal TotalAmount,
    DateTimeOffset GeneratedAtUtc, DateTimeOffset? FinalizedAtUtc,
    IReadOnlyList<InvoiceLineItemDto> LineItems);

public record GenerateDraftInvoiceRequest(DateOnly PeriodStart, DateOnly PeriodEnd, decimal? ManualExchangeRate = null);

public record FinalizeInvoiceRequest(string InvoiceNumber);

public record ApplyLineItemDiscountRequest(decimal? DiscountPercent);

/// <summary>FDD: "Project managers can view the staged invoice for their projects" - a read-only view scoped
/// to just the caller's own managed project(s). LineItems here are already filtered down to those projects,
/// and MyTotalAmount is the sum of just those lines (never the invoice's own, possibly-broader, TotalAmount -
/// that would leak the value of other projects' work on the same invoice to a PM who shouldn't see it).</summary>
public record ProjectManagerInvoiceDto(
    int Id, int ClientId, string ClientName, DateOnly PeriodStart, DateOnly PeriodEnd,
    string ReportingCurrency, string Status, string? InvoiceNumber, decimal MyTotalAmount,
    DateTimeOffset GeneratedAtUtc, DateTimeOffset? FinalizedAtUtc,
    IReadOnlyList<InvoiceLineItemDto> LineItems);
