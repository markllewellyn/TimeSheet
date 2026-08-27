namespace TimeSheet.Domain.Entities;

/// <summary>
/// Maps onto the legacy [RecordedTimes] table. ClientId is now denormalized directly onto the row (matching the
/// legacy shape) rather than derived via Project.ClientId. ToPayroll/ToCompany and the ApprovedPayroll/
/// SentToPayroll/PostingBatch payroll-workflow columns are present for schema fidelity but not yet wired to any
/// business logic - nothing in the app reads or writes them beyond a default value. ExpensesValue is likewise
/// present but deliberately unused - expenses are tracked via the separate ExpenseEntry table instead.
/// ModifiedUtc extends the legacy table directly (no legacy equivalent, too small to warrant a split table).
/// An entry is never blocked or gated - see EntryFlag for the FDD's non-blocking "flag for query" model.
/// </summary>
public class TimesheetEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int ClientId { get; set; }
    public Client? Client { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public DateOnly Date { get; set; }
    public decimal WorkHours { get; set; }
    public decimal OutOfHoursHours { get; set; }
    public required string Description { get; set; }

    /// <summary>Precomputed monetary amounts (legacy [RecordedTimes].ToPayroll/ToCompany), computed at write
    /// time from the resolved rate/cost below - see TimesheetEntriesFunctions.ComputePayrollAmountsAsync.</summary>
    public decimal ToPayroll { get; set; }
    public decimal ToCompany { get; set; }

    /// <summary>The rate/cost actually resolved and applied to this entry, snapshotted at Create/Update/
    /// Duplicate time using the entry's own Date (never "now") - so a later rate change never retroactively
    /// affects an entry that isn't itself re-saved. This is what lets an entry "always show which rate was
    /// charged and why" (FDD), and lets Reporting/Invoicing sum these instead of re-resolving at report time.
    /// All nullable since entries predating this feature have no snapshot.</summary>
    public decimal? ResolvedCustomerRate { get; set; }
    public decimal? ResolvedHourlyCost { get; set; }
    public decimal? ResolvedOutOfHoursCost { get; set; }
    public int? RateCardId { get; set; }
    public RateCard? RateCard { get; set; }
    public int? StaffCostId { get; set; }
    public StaffCost? StaffCost { get; set; }
    public RateCardTier? Tier { get; set; }

    public bool ApprovedPayroll { get; set; }
    public int? ApprovedByStaffId { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTimeOffset? DateApprovedPayroll { get; set; }
    public bool SentToPayroll { get; set; }
    public int? SentByStaffId { get; set; }
    public string? SentByName { get; set; }
    public DateTimeOffset? DateSentToPayroll { get; set; }
    public string? PostingBatch { get; set; }

    /// <summary>Legacy [RecordedTimes].ExpensesValue - deliberately unused; expenses are tracked via ExpenseEntry instead.</summary>
    public decimal? ExpensesValue { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>Null for the overwhelming common case (a person logging their own time). Set only when an
    /// Admin creates an entry on someone else's behalf (impersonation) - UserId still identifies whose
    /// timesheet the entry appears on; this identifies who actually submitted it, so the two are never
    /// silently conflated. See TimesheetEntriesFunctions.Create.</summary>
    public int? CreatedByUserId { get; set; }

    public List<Attachment> Attachments { get; set; } = [];
}
