namespace TimeSheet.Domain.Entities;

/// <summary>One staff member's aggregated out-of-hours pay within a PayrollPeriod. OutOfHoursPay is
/// OutOfHoursHours summed against each contributing entry's own ResolvedOutOfHoursCost (the staff-payout rate
/// as it stood on the entry's own Date, never re-resolved) - see PayrollAggregationService.</summary>
public class PayrollPeriodLine
{
    public int Id { get; set; }
    public int PayrollPeriodId { get; set; }
    public PayrollPeriod? PayrollPeriod { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public decimal OutOfHoursHours { get; set; }
    public decimal OutOfHoursPay { get; set; }
}
