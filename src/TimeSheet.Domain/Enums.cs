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
