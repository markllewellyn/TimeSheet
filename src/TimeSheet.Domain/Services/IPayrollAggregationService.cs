using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Services;

/// <summary>FDD Payroll: "Out of hours hours are aggregated per staff member for the payroll period and priced
/// at that staff member's out of hours rate as it stood at the date of the entry." Notify-only - never mutates
/// ApprovedPayroll/SentToPayroll on the source entries; sending to payroll stays a deliberate manual action via
/// the existing Approvals_SendToPayroll endpoint.</summary>
public interface IPayrollAggregationService
{
    Task<PayrollPeriod> AggregateAsync(DateOnly periodStart, DateOnly periodEnd, CancellationToken ct);
}
