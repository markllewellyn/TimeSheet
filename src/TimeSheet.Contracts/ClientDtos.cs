namespace TimeSheet.Contracts;

public record ClientDto(
    int Id, string Name, string AccountCode, DateOnly StartDate,
    string? BillingAddressLine1, string? BillingAddressLine2, string? BillingCity, string? BillingPostalCode, string? BillingCountryCode,
    string? PrimaryContactName, string? PrimaryContactEmail, string? PrimaryContactPhone,
    int? CurrencyId, string ReportingCurrencyCode, int? InvoicingMonthEndDay, string? Notes, bool IsActive,
    string BillingPeriod, DateOnly? CurrentPeriodStart, DateOnly? CurrentPeriodEnd);

/// <summary>BillingPeriod is optional - omitted or unrecognized defaults to "OneOff" (see ClientsFunctions).
/// CurrentPeriodStart/End are only meaningful when BillingPeriod is "Monthly"; when omitted there, they default
/// server-side to the current calendar month rather than erroring.</summary>
public record UpsertClientRequest(
    string Name, string AccountCode, DateOnly StartDate,
    string? BillingAddressLine1, string? BillingAddressLine2, string? BillingCity, string? BillingPostalCode, string? BillingCountryCode,
    string? PrimaryContactName, string? PrimaryContactEmail, string? PrimaryContactPhone,
    int? CurrencyId, int? InvoicingMonthEndDay, string? Notes,
    string? BillingPeriod = null, DateOnly? CurrentPeriodStart = null, DateOnly? CurrentPeriodEnd = null);
