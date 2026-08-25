namespace TimeSheet.Domain.Entities;

public class Client
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string AccountCode { get; set; }

    public string? BillingAddressLine1 { get; set; }
    public string? BillingAddressLine2 { get; set; }
    public string? BillingCity { get; set; }
    public string? BillingPostalCode { get; set; }
    public string? BillingCountryCode { get; set; }

    public string? PrimaryContactName { get; set; }
    public string? PrimaryContactEmail { get; set; }
    public string? PrimaryContactPhone { get; set; }

    /// <summary>ISO 4217 code. Resolution order for money display is Project.CurrencyOverride ?? Client.ReportingCurrencyCode.</summary>
    public required string ReportingCurrencyCode { get; set; }

    /// <summary>Day of month the invoicing period ends on for this client. Null = calendar month end.</summary>
    public int? InvoicingMonthEndDay { get; set; }

    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? ModifiedUtc { get; set; }
    public int? ModifiedByUserId { get; set; }

    public List<Project> Projects { get; set; } = [];
    public List<ClientAccountManager> AccountManagers { get; set; } = [];
}
