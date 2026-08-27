using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class StaffCostRepository(TimesheetDbContext db) : IStaffCostRepository
{
    public Task<StaffCost?> GetByIdAsync(int id, CancellationToken ct) =>
        db.StaffCosts.Include(sc => sc.Staff).FirstOrDefaultAsync(sc => sc.Id == id, ct);

    public Task<StaffCost?> GetCurrentAsync(int staffId, DateOnly asOfDate, CancellationToken ct) =>
        db.StaffCosts
            .Where(sc => sc.StaffId == staffId && sc.EffectiveFrom <= asOfDate)
            .OrderByDescending(sc => sc.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<StaffCost>> GetByStaffAsync(int staffId, CancellationToken ct)
    {
        var rows = await db.StaffCosts.Where(sc => sc.StaffId == staffId).ToListAsync(ct);
        return rows.OrderByDescending(sc => sc.EffectiveFrom).ToList();
    }

    public async Task AddAsync(StaffCost cost, CancellationToken ct) => await db.StaffCosts.AddAsync(cost, ct);
}
