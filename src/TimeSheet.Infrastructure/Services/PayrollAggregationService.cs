using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

/// <summary>Sums OutOfHoursHours * ResolvedOutOfHoursCost (the staff-payout rate, snapshotted per entry as it
/// stood on the entry's own Date - never re-resolved) per staff member, across entries already approved for
/// payroll but not yet sent. Deliberately NOT ToPayroll/ToCompany - see TimesheetEntriesFunctions.
/// ComputePayrollAmountsAsync: ToPayroll is priced at the client-facing CustomerRate and blends regular + OOH
/// hours, so despite its name it is not the amount paid to staff for out-of-hours work.</summary>
public class PayrollAggregationService(
    ITimesheetEntryRepository entries,
    IPayrollPeriodRepository payrollPeriods,
    IUnitOfWork uow) : IPayrollAggregationService
{
    public async Task<PayrollPeriod> AggregateAsync(DateOnly periodStart, DateOnly periodEnd, CancellationToken ct)
    {
        var outOfHoursEntries = await entries.GetOutOfHoursApprovedNotSentAsync(periodStart, periodEnd, ct);

        var lines = outOfHoursEntries
            .GroupBy(e => e.UserId)
            .Select(g => new PayrollPeriodLine
            {
                UserId = g.Key,
                OutOfHoursHours = g.Sum(e => e.OutOfHoursHours),
                OutOfHoursPay = g.Sum(e => e.OutOfHoursHours * (e.ResolvedOutOfHoursCost ?? 0)),
            })
            .ToList();

        var period = await payrollPeriods.GetByPeriodStartAsync(periodStart, ct);
        if (period is not null)
        {
            payrollPeriods.ClearLines(period);
            period.PeriodEnd = periodEnd;
            period.GeneratedAtUtc = DateTimeOffset.UtcNow;
            period.TotalOutOfHoursHours = lines.Sum(l => l.OutOfHoursHours);
            period.TotalOutOfHoursPay = lines.Sum(l => l.OutOfHoursPay);
            foreach (var line in lines) period.Lines.Add(line);
            payrollPeriods.Update(period);
        }
        else
        {
            period = new PayrollPeriod
            {
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                TotalOutOfHoursHours = lines.Sum(l => l.OutOfHoursHours),
                TotalOutOfHoursPay = lines.Sum(l => l.OutOfHoursPay),
                Lines = lines,
            };
            await payrollPeriods.AddAsync(period, ct);
        }

        await uow.SaveChangesAsync(ct);
        return period;
    }
}
