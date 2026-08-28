namespace TimeSheet.Domain.Entities;

/// <summary>
/// An effective-dated GBP hourly billing rate, scoped to a role or to an individual, optionally narrowed to a
/// specific client or a specific project - genuinely new, no legacy equivalent. Exactly one of RoleId/StaffId is
/// set (role-tier vs person-tier); when StaffId is set, exactly one of ClientId/ProjectId is set (person+client
/// or person+project - there is no "staff-global" shape, that's not one of the 5 resolution tiers). A rate
/// change is always a NEW row with a later EffectiveFrom, never a mutation of an existing row - there is no
/// EffectiveTo; resolution takes the highest EffectiveFrom &lt;= the date being resolved for a matching scope.
/// See IRateResolver for the 5-tier resolution order.
/// </summary>
public class RateCard
{
    public int Id { get; set; }

    public int? RoleId { get; set; }
    public Role? Role { get; set; }
    public int? StaffId { get; set; }
    public User? Staff { get; set; }
    public int? ClientId { get; set; }
    public Client? Client { get; set; }
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    public decimal Rate { get; set; }

    /// <summary>FDD: "A discount can be applied... against a rate card before the invoice is produced." 0-100,
    /// null = no discount. Applied by IRateResolver at resolution time (baked into the CustomerRate it
    /// returns), never re-applied later at invoice time - see RateResolver.ResolveAsync.</summary>
    public decimal? DiscountPercent { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
}
