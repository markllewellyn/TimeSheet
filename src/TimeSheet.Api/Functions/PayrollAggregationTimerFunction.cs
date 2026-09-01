using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TimeSheet.Domain;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Monthly out-of-hours payroll aggregation (FDD: "out of hours hours are aggregated per staff member
/// for the payroll period"). Aggregate-and-notify only - never marks source entries SentToPayroll; an admin
/// still sends to payroll manually via the existing Approvals_SendToPayroll endpoint. Notification/logging live
/// here rather than in PayrollAggregationService so the service stays testable without a notification mock -
/// mirrors ProjectHealthTimerFunction owning its own summarizing around a pure-work service call.</summary>
public class PayrollAggregationTimerFunction(
    IPayrollAggregationService payrollAggregation,
    INotificationService notificationService,
    ILogger<PayrollAggregationTimerFunction> logger)
{
    [Function("MonthlyPayrollAggregation")]
    public async Task Run([TimerTrigger("0 30 3 1 * *")] TimerInfo timer, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
        var periodEnd = new DateOnly(today.Year, today.Month, 1).AddDays(-1); // last day of the PREVIOUS month
        var periodStart = new DateOnly(periodEnd.Year, periodEnd.Month, 1);

        var period = await payrollAggregation.AggregateAsync(periodStart, periodEnd, ct);

        if (period.Lines.Count > 0)
        {
            await notificationService.RaiseToAdminsAsync(
                NotificationType.PayrollPeriodReady,
                $"Out-of-hours payroll aggregation for {periodStart:MMMM yyyy} is ready: " +
                $"{period.TotalOutOfHoursHours}h totalling {period.TotalOutOfHoursPay:C} across {period.Lines.Count} staff. " +
                "Review and send to payroll via Approvals.",
                NotificationChannel.Both,
                ct: ct);
        }

        logger.LogInformation("Monthly payroll aggregation complete for {PeriodStart}..{PeriodEnd}: {Lines} staff, {Hours}h.",
            periodStart, periodEnd, period.Lines.Count, period.TotalOutOfHoursHours);
    }
}
