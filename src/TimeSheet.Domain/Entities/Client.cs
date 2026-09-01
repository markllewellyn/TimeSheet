namespace TimeSheet.Domain.Entities;

/// <summary>
/// Maps onto the legacy [Customers] table (Id/Name/AccountCode/StartDate/IsActive/CurrencyId) plus a table-split
/// partner [CustomerProfiles] holding everything the legacy table has no room for (billing/contact/notes/audit
/// fields) - see ClientConfiguration. StartDate and CurrencyId are legacy-required columns with no equivalent
/// in the app's original design; StartDate is backfilled from CreatedUtc for pre-existing rows.
/// </summary>
public class Client
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string AccountCode { get; set; }
    public DateOnly StartDate { get; set; }

    public string? BillingAddressLine1 { get; set; }
    public string? BillingAddressLine2 { get; set; }
    public string? BillingCity { get; set; }
    public string? BillingPostalCode { get; set; }
    public string? BillingCountryCode { get; set; }

    public string? PrimaryContactName { get; set; }
    public string? PrimaryContactEmail { get; set; }
    public string? PrimaryContactPhone { get; set; }

    /// <summary>Legacy [Customers].PK_Currency - a real FK, replacing what used to be a plain stored ISO code.</summary>
    public int? CurrencyId { get; set; }
    public Currency? Currency { get; set; }

    /// <summary>ISO 4217 code, resolved via the Currency FK. Resolution order for money display is
    /// Project.CurrencyOverride ?? Client.ReportingCurrencyCode. Callers loading a Client for money math must
    /// .Include(c => c.Currency) or this silently falls back to "GBP".</summary>
    public string ReportingCurrencyCode => Currency?.CurrencyCode ?? "GBP";

    /// <summary>Day of month the invoicing period ends on for this client. Null = calendar month end.</summary>
    public int? InvoicingMonthEndDay { get; set; }

    /// <summary>FDD Key Entities: "A client, with... the billing period." OneOff (default, preserves every
    /// existing client's behavior) = an admin generates invoices manually as today. Monthly = the
    /// MonthlyBillingRollForward timer auto-generates a Draft for CurrentPeriodStart/End once it has closed,
    /// then advances the window - see IBillingRollForwardService. CurrentPeriodStart/End are only meaningful
    /// when BillingPeriod is Monthly.</summary>
    public BillingPeriod BillingPeriod { get; set; } = BillingPeriod.OneOff;
    public DateOnly? CurrentPeriodStart { get; set; }
    public DateOnly? CurrentPeriodEnd { get; set; }

    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? ModifiedUtc { get; set; }
    public int? ModifiedByUserId { get; set; }

    public List<Project> Projects { get; set; } = [];
    public List<ClientAccountManager> AccountManagers { get; set; } = [];
}
