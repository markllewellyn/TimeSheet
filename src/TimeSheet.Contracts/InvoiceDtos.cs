namespace TimeSheet.Contracts;

public record InvoiceLineItemDto(int Id, int ProjectId, string ProjectName, string Description, decimal? Hours, decimal Amount, string Type);

public record InvoiceDto(
    int Id, int ClientId, string ClientName, DateOnly PeriodStart, DateOnly PeriodEnd,
    string ReportingCurrency, decimal? ExchangeRate, string Status, string? InvoiceNumber, decimal TotalAmount,
    DateTimeOffset GeneratedAtUtc, DateTimeOffset? FinalizedAtUtc,
    IReadOnlyList<InvoiceLineItemDto> LineItems);

public record GenerateDraftInvoiceRequest(DateOnly PeriodStart, DateOnly PeriodEnd, decimal? ManualExchangeRate = null);

public record FinalizeInvoiceRequest(string InvoiceNumber);
