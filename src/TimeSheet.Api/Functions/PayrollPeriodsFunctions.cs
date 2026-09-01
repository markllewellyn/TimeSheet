using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Read-only view of the periods MonthlyPayrollAggregation has generated (see
/// PayrollAggregationService) - Admin-only throughout, like Invoicing/Reports, since PayrollPeriod has no
/// project dimension to scope a project manager against (unlike the Approvals queue).</summary>
public class PayrollPeriodsFunctions(IPayrollPeriodRepository payrollPeriods, ICurrentUserAccessor currentUser)
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

    private static PayrollPeriodDto ToDto(PayrollPeriod p) => new(
        p.Id, p.PeriodStart, p.PeriodEnd, p.GeneratedAtUtc, p.TotalOutOfHoursHours, p.TotalOutOfHoursPay, p.Lines.Count);

    private static PayrollPeriodDetailDto ToDetailDto(PayrollPeriod p) => new(
        p.Id, p.PeriodStart, p.PeriodEnd, p.GeneratedAtUtc, p.TotalOutOfHoursHours, p.TotalOutOfHoursPay,
        p.Lines
            .Select(l => new PayrollPeriodLineDto(l.UserId, l.User?.DisplayName ?? "", l.OutOfHoursHours, l.OutOfHoursPay))
            .OrderBy(l => l.StaffName)
            .ToList());
}
