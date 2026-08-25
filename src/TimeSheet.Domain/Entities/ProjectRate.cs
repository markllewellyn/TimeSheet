namespace TimeSheet.Domain.Entities;

/// <summary>
/// A single temporal rate-card table for a Project: UserId null = project-wide default, set = per-user override.
/// Both project-default and per-user rows are date-ranged so historical timesheet entries always bill/cost
/// at the rate that was in force on the date the work was done.
/// </summary>
public class ProjectRate
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    /// <summary>Null = project-wide default rate. Non-null = override for this specific user on this project.</summary>
    public int? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Revenue side. Required (service-layer validated) when Project.PaymentModel == TimeAndMaterials; optional for FixedProjectCost.</summary>
    public decimal? BillingRatePerHour { get; set; }

    /// <summary>Internal cost basis. Always required — cost accrues by hours worked regardless of how the client is billed.</summary>
    public decimal CostRatePerHour { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    /// <summary>Null = open-ended / currently in force.</summary>
    public DateOnly? EffectiveTo { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
}
