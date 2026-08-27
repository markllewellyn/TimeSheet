namespace TimeSheet.Domain.Entities;

/// <summary>
/// Effective-dated internal hourly cost for a staff member (per the FDD's "StaffCost" - NOT the old,
/// per-(Staff,Client) billing-rate entity this class used to be; that concept is now RateCard). A change is
/// always a NEW row with a later EffectiveFrom, never a mutation - resolution takes the row with the highest
/// EffectiveFrom &lt;= the date being resolved. HourlyCost replaces the old flat User.HourlyCost; OutOfHoursCost
/// is the staff-payout rate for out-of-hours work (billed to the client at the same RateCard rate as regular
/// hours - only the amount PAID TO STAFF differs for OOH hours).
/// </summary>
public class StaffCost
{
    public int Id { get; set; }
    public int StaffId { get; set; }
    public User? Staff { get; set; }

    public decimal HourlyCost { get; set; }
    public decimal OutOfHoursCost { get; set; }
    public DateOnly EffectiveFrom { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
}
