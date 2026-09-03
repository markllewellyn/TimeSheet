namespace TimeSheet.Domain.Entities;

/// <summary>
/// Maps onto the legacy [Projects] table (Id/ClientId/Name/IsActive/CanInvoice) plus a table-split partner
/// [ProjectDetails] holding everything the legacy table has no room for (Code/Description/PaymentModel/budget
/// fields/dates/audit fields) - see ProjectConfiguration. Rates no longer live on Project - see StaffCost,
/// which is keyed by (Staff, Client) rather than (Staff, Project).
/// </summary>
public class Project
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public Client? Client { get; set; }

    public required string Name { get; set; }

    /// <summary>Unique per-client, not globally (mirrors the legacy app's project codes, e.g. "FDD-3145").</summary>
    public required string Code { get; set; }
    public string? Description { get; set; }

    public PaymentModel PaymentModel { get; set; }

    /// <summary>FDD: Development/Support/Contract - orthogonal to PaymentModel. Gates whether a Contract
    /// EntryType may be configured against this project - see EntryType.</summary>
    public ProjectType ProjectType { get; set; }

    /// <summary>Legacy [Projects].CanInvoice - present for schema fidelity, not yet wired to any invoicing logic.</summary>
    public bool? CanInvoice { get; set; }

    /// <summary>FDD: "A project manager is nominated against each project." Null until an admin sets one.
    /// Grants extra (non-admin) visibility: querying/clearing this project's EntryFlags, viewing this
    /// project's lines on a staged invoice, and seeing this project's EstimatedCost/Profit - see
    /// AuthorizationExtensions.RequireAdminOrProjectManager.</summary>
    public int? ProjectManagerUserId { get; set; }
    public User? ProjectManager { get; set; }

    /// <summary>Project-level currency override. When null, the project inherits Client.ReportingCurrencyCode.</summary>
    public string? CurrencyOverride { get; set; }

    public DateOnly StartDate { get; set; }

    /// <summary>Null = open-ended / retainer engagement.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>The "time limit" budget ceiling in hours (either payment model).</summary>
    public decimal? BudgetHours { get; set; }

    /// <summary>Only meaningful when PaymentModel == FixedProjectCost.</summary>
    public decimal? FixedFeeAmount { get; set; }

    /// <summary>The highest of AppSettings' two firm-wide notification thresholds (50/75 by default) already
    /// notified-for on this project, so a notification fires once per newly-crossed threshold rather than on
    /// every subsequent entry once past it - null until the first threshold is crossed. See
    /// BudgetMonitoringService.</summary>
    public int? HighestBudgetNotificationPercent { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? ModifiedUtc { get; set; }

    public List<StaffProject> Assignments { get; set; } = [];

    /// <summary>FDD: "Attachments and documents can be held against a project." See ProjectAttachment.</summary>
    public List<ProjectAttachment> Attachments { get; set; } = [];

    public string GetEffectiveCurrency(string clientReportingCurrency) => CurrencyOverride ?? clientReportingCurrency;
}
