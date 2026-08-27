using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IStaffCostRepository
{
    Task<StaffCost?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>The effective-dated row for this staff member with the highest EffectiveFrom &lt;= asOfDate -
    /// null if none exists (they have no cost configured as of that date). See RateResolver.</summary>
    Task<StaffCost?> GetCurrentAsync(int staffId, DateOnly asOfDate, CancellationToken ct);

    /// <summary>All dated rows for one person, newest first - for the Staff screen's history view.</summary>
    Task<IReadOnlyList<StaffCost>> GetByStaffAsync(int staffId, CancellationToken ct);

    Task AddAsync(StaffCost cost, CancellationToken ct);
}
