using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IPayrollPeriodRepository
{
    /// <summary>For the monthly aggregation timer's idempotent re-run - a PeriodStart already generated is
    /// rebuilt in place via ClearLines rather than duplicated.</summary>
    Task<PayrollPeriod?> GetByPeriodStartAsync(DateOnly periodStart, CancellationToken ct);

    Task AddAsync(PayrollPeriod period, CancellationToken ct);
    void Update(PayrollPeriod period);

    /// <summary>Clears an existing period's lines so it can be rebuilt from scratch - mirrors
    /// IInvoiceRepository.ClearLineItems.</summary>
    void ClearLines(PayrollPeriod period);
}
