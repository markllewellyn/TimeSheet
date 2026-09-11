namespace TimeSheet.Domain.Entities;

/// <summary>
/// A plain staff grouping (e.g. "Delivery Pod A", "North Region") - FDD: reporting "on a team, role and user
/// basis." Deliberately mirrors Role.cs exactly: admin-managed, a person has at most one current Team
/// (User.TeamId), no rate/billing significance of any kind (unlike Role, which RateCard resolves against) -
/// Team exists purely as a second, independent reporting/filtering axis alongside Role and User.
/// </summary>
public class Team
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; }
}
