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

public enum AssignmentStatus
{
    Active,
    Paused,
    Ended
}

public enum TimesheetEntryStatus
{
    Normal,
    PendingApproval,
    Approved,
    Declined
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
    EscalationRaised,
    EscalationDecided,
    InvoiceGenerated,
    ProjectHealthDeclined
}

public enum NotificationChannel
{
    InAppOnly,
    EmailOnly,
    Both
}

public enum EscalationReason
{
    ProjectBudgetExceeded,
    UserAllocationExceeded
}

public enum EscalationDecision
{
    Pending,
    Approved,
    Declined
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
