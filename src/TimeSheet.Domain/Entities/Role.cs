namespace TimeSheet.Domain.Entities;

/// <summary>
/// A job function (e.g. "Developer", "Consultant") that a RateCard can be attached to - genuinely new, no
/// legacy equivalent. Deliberately NOT effective-dated: a staff member has a single current Role
/// (User.JobRoleId), so resolving a role-tier rate for a past date implicitly assumes "role today = role back
/// then". Accepted limitation of keeping Role scope global rather than per-project-assignment.
/// </summary>
public class Role
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; }
}
