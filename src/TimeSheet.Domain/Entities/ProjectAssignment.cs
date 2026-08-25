namespace TimeSheet.Domain.Entities;

/// <summary>
/// User &lt;-&gt; Project junction (confirmed many-to-many: one user can be assigned across multiple clients' projects).
/// Uses a surrogate key (not a composite PK) so a user can be re-assigned to the same project after being
/// rolled off, preserving history as separate rows rather than overwriting.
/// </summary>
public class ProjectAssignment
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }

    public AssignmentStatus Status { get; set; } = AssignmentStatus.Active;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Explicit staffing input driving the "estimated hours this week" widget. Deliberately NOT derived from
    /// remaining budget / remaining weeks — see IUserWorkloadService for the reasoning.
    /// </summary>
    public decimal? AllocatedHoursPerWeek { get; set; }

    public string? Notes { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
}
