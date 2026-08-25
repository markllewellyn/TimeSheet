namespace TimeSheet.Contracts;

public record InvoiceLineItemDto(int Id, int ProjectId, string ProjectName, string Description, decimal? Hours, decimal Amount, string Type);

public record InvoiceDto(
    int Id, int ClientId, string ClientName, DateOnly PeriodStart, DateOnly PeriodEnd,
    string ReportingCurrency, string Status, string? InvoiceNumber, decimal TotalAmount,
    DateTimeOffset GeneratedAtUtc, DateTimeOffset? FinalizedAtUtc,
    IReadOnlyList<InvoiceLineItemDto> LineItems);

public record GenerateDraftInvoiceRequest(DateOnly PeriodStart, DateOnly PeriodEnd);

public record FinalizeInvoiceRequest(string InvoiceNumber);
