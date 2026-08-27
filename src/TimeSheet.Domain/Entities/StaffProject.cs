namespace TimeSheet.Domain.Entities;

/// <summary>
/// Maps onto the legacy [StaffProjects] table (replaces the old ProjectAssignment) plus a table-split partner
/// [StaffProjectDetails] holding StartDate/EndDate/Notes, which have no legacy equivalent - see
/// StaffProjectConfiguration. Links directly to a Staff member (not mediated through a rate-configuration row,
/// unlike before this class's StaffCostId was retired) - assigning someone to a project only requires that
/// IRateResolver can resolve SOME rate for them (a role-tier default counts), not that a specific override
/// exists - see ProjectAssignmentsFunctions for the validation.
/// AllocatedHoursPerWeek extends the legacy table directly (no legacy equivalent, needed by the "estimated
/// hours this week" widget). The old Active/Paused/Ended Status enum collapses to the legacy IsActive bit -
/// Paused and Ended both become IsActive=false; the distinction is lost.
/// </summary>
public class StaffProject
{
    public int Id { get; set; }
    public int StaffId { get; set; }
    public User? Staff { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Explicit staffing input driving the "estimated hours this week" widget. Deliberately NOT derived from
    /// remaining budget / remaining weeks — see IUserWorkloadService for the reasoning.
    /// </summary>
    public decimal? AllocatedHoursPerWeek { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
}
