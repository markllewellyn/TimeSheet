namespace TimeSheet.Contracts;

public record ClientDto(
    int Id, string Name, string AccountCode,
    string? BillingAddressLine1, string? BillingAddressLine2, string? BillingCity, string? BillingPostalCode, string? BillingCountryCode,
    string? PrimaryContactName, string? PrimaryContactEmail, string? PrimaryContactPhone,
    string ReportingCurrencyCode, int? InvoicingMonthEndDay, string? Notes, bool IsActive);

public record UpsertClientRequest(
    string Name, string AccountCode,
    string? BillingAddressLine1, string? BillingAddressLine2, string? BillingCity, string? BillingPostalCode, string? BillingCountryCode,
    string? PrimaryContactName, string? PrimaryContactEmail, string? PrimaryContactPhone,
    string ReportingCurrencyCode, int? InvoicingMonthEndDay, string? Notes);
