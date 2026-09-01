using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Read-only view of the periods MonthlyPayrollAggregation has generated (see
/// PayrollAggregationService) - Admin-only throughout, like Invoicing/Reports, since PayrollPeriod has no
/// project dimension to scope a project manager against (unlike the Approvals queue).</summary>
public class PayrollPeriodsFunctions(
    IPayrollPeriodRepository payrollPeriods,
    IPayrollAggregationService payrollAggregation,
    INotificationService notificationService,
    ICurrentUserAccessor currentUser)
{
    [Function("PayrollPeriods_List")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "payroll-periods")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var periods = await payrollPeriods.GetAllAsync(ct);
        return new OkObjectResult(periods.Select(ToDto));
    }

    [Function("PayrollPeriods_Get")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "payroll-periods/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var period = await payrollPeriods.GetByIdAsync(id, ct);
        return period is null ? new NotFoundResult() : new OkObjectResult(ToDetailDto(period));
    }

    /// <summary>Manual trigger for a demo/admin who doesn't want to wait for the 1st of the month - runs the
    /// exact same aggregation the MonthlyPayrollAggregation timer does (same period bounds, same
    /// notify-if-nonempty behavior), so behavior is identical regardless of what triggered it.</summary>
    [Function("PayrollPeriods_RunNow")]
    public async Task<IActionResult> RunNow(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "payroll-periods/run-now")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

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

        return new OkObjectResult(ToDto(period));
    }

    private static PayrollPeriodDto ToDto(PayrollPeriod p) => new(
        p.Id, p.PeriodStart, p.PeriodEnd, p.GeneratedAtUtc, p.TotalOutOfHoursHours, p.TotalOutOfHoursPay, p.Lines.Count);

    private static PayrollPeriodDetailDto ToDetailDto(PayrollPeriod p) => new(
        p.Id, p.PeriodStart, p.PeriodEnd, p.GeneratedAtUtc, p.TotalOutOfHoursHours, p.TotalOutOfHoursPay,
        p.Lines
            .Select(l => new PayrollPeriodLineDto(l.UserId, l.User?.DisplayName ?? "", l.OutOfHoursHours, l.OutOfHoursPay))
            .OrderBy(l => l.StaffName)
            .ToList());
}
