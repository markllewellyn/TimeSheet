using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class PayrollPeriodRepository(TimesheetDbContext db) : IPayrollPeriodRepository
{
    public Task<PayrollPeriod?> GetByPeriodStartAsync(DateOnly periodStart, CancellationToken ct) =>
        db.PayrollPeriods.Include(p => p.Lines).FirstOrDefaultAsync(p => p.PeriodStart == periodStart, ct);

    public async Task AddAsync(PayrollPeriod period, CancellationToken ct) => await db.PayrollPeriods.AddAsync(period, ct);

    public void Update(PayrollPeriod period) => db.PayrollPeriods.Update(period);

    public void ClearLines(PayrollPeriod period)
    {
        db.PayrollPeriodLines.RemoveRange(period.Lines);
        period.Lines.Clear();
    }
}
