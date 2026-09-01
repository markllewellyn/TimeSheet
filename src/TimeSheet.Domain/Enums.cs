namespace TimeSheet.Domain;

public enum UserRole
{
    Admin,
    User
}

public enum PaymentModel
{
    TimeAndMaterials,
    FixedProjectCost
}

/// <summary>FDD Key Entities: "A client, with... the billing period." OneOff = an admin generates invoices
/// manually as today (default, preserves existing behavior for every client). Monthly = the
/// MonthlyBillingRollForward timer auto-generates a Draft invoice once Client.CurrentPeriodEnd has closed, then
/// advances the window - see IBillingRollForwardService.</summary>
public enum BillingPeriod
{
    OneOff,
    Monthly
}

/// <summary>FDD: "Projects are split by type: Development, Support and Contract" - a different axis from
/// PaymentModel (which controls how a project is billed, not what kind of work it is). Only a Contract-type
/// project may have a Contract EntryType configured against it - see EntryType.</summary>
public enum ProjectType
{
    Development,
    Support,
    Contract
}

public enum AssignmentStatus
{
    Active,
    Paused,
    Ended
}

public enum InvoiceStatus
{
    Draft,
    Finalized
}

public enum InvoiceLineItemType
{
    TimeAndMaterials,
    FixedFee,
    Expense
}

public enum NotificationType
{
    TimesheetReminder,
    BudgetWarning,
    BudgetExceeded,
    // Unused going forward (replaced by FlagRaised/FlagCleared) - kept so historical Notification rows
    // (Type is HasConversion<string>()) still deserialize.
    EscalationRaised,
    EscalationDecided,
    InvoiceGenerated,
    ProjectHealthDeclined,
    FlagRaised,
    FlagCleared,
    PayrollPeriodReady
}

public enum NotificationChannel
{
    InAppOnly,
    EmailOnly,
    Both
}

/// <summary>Why an EntryFlag was raised. ProjectBudgetExceeded/UserAllocationExceeded are system-raised (see
/// BudgetMonitoringService); Manual is an admin/PM flagging an entry for query without a triggering condition.</summary>
public enum EntryFlagReason
{
    ProjectBudgetExceeded,
    UserAllocationExceeded,
    Manual
}

public enum ProjectHealthStatus
{
    OnTrack,
    AtRisk,
    Behind
}

/// <summary>Distinguishes a staff expense claim (billable/reimbursable) from an Admin-only "Contract" value -
/// a monetary figure logged against a non-invoiceable project, mirroring the legacy Power App's separate
/// admin-only value-entry flow (see ExpenseEntriesFunctions.Create).</summary>
public enum ExpenseEntryKind
{
    Expense,
    Contract
}

/// <summary>Which of RateCard's 5 resolution tiers produced the rate stamped on a TimesheetEntry, most
/// specific first - see IRateResolver.</summary>
public enum RateCardTier
{
    PersonProject,
    PersonClient,
    RoleProject,
    RoleClient,
    RoleDefault
}
