namespace TimeSheet.Domain.Entities;

/// <summary>FDD Key Entities: "Aggregated out of hours time per staff member for a payroll period." Genuinely
/// new shape (no legacy mirroring, same category as StaffCost) - built by IPayrollAggregationService's monthly
/// timer, never hand-edited. Re-running the timer for an already-generated month rebuilds this row's Lines in
/// place rather than creating a duplicate - see PayrollPeriodConfiguration's unique index on PeriodStart.</summary>
public class PayrollPeriod
{
    public int Id { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public decimal TotalOutOfHoursHours { get; set; }
    public decimal TotalOutOfHoursPay { get; set; }

    public List<PayrollPeriodLine> Lines { get; set; } = [];
}
