namespace TimeSheet.Domain.Services;

/// <summary>The rate/cost actually resolved for a (staff, client, project, date) combination - both the
/// client-billing side (CustomerRate, from RateCard's 5-tier hierarchy) and the internal-cost side (HourlyCost/
/// OutOfHoursCost, from the effective-dated StaffCost) in one call, since a TimesheetEntry needs both stamped
/// at save time. RateCardId/StaffCostId identify which dated row produced each half - see IRateResolver.
/// StaffCostId is null when the cost half was skipped entirely (Project.IsCostExempt), not when a lookup
/// simply matched no rows - that case still throws.</summary>
public record RateResolution(decimal CustomerRate, decimal HourlyCost, decimal OutOfHoursCost, int RateCardId, int? StaffCostId, RateCardTier Tier);

public interface IRateResolver
{
    /// <summary>Resolves via RateCard's 5-tier hierarchy (person+project, person+client, role+project,
    /// role+client, role default - first match wins) plus the effective-dated StaffCost lookup, both as of
    /// asOfDate - not "now" - so a later rate/cost change never retroactively affects an entry unless that
    /// entry is itself re-saved. Throws RateNotConfiguredException if the rate-card half can't be resolved, or
    /// if the cost half can't be resolved AND isCostExempt is false - a real data-entry gap (assign a Role, set
    /// up an override, or configure a StaffCost). When isCostExempt is true the cost half is skipped entirely
    /// and resolves to a deliberate zero instead - see Project.IsCostExempt.</summary>
    Task<RateResolution> ResolveAsync(int staffId, int clientId, int projectId, DateOnly asOfDate, bool isCostExempt, CancellationToken ct);
}

public class RateNotConfiguredException : Exception
{
    private RateNotConfiguredException(string message) : base(message)
    {
    }

    public static RateNotConfiguredException NoRateCard(int staffId, int clientId, int projectId) =>
        new($"No RateCard could be resolved for staff {staffId}, client {clientId}, project {projectId} - assign a Role, or set up a rate override.");

    public static RateNotConfiguredException NoStaffCost(int staffId) =>
        new($"No StaffCost (internal hourly cost) configured for staff {staffId}.");
}
