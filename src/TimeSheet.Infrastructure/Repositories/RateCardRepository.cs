using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class RateCardRepository(TimesheetDbContext db) : IRateCardRepository
{
    public Task<RateCard?> FindAsync(int? roleId, int? staffId, int? clientId, int? projectId, DateOnly asOfDate, CancellationToken ct) =>
        db.RateCards
            .Where(rc => rc.RoleId == roleId && rc.StaffId == staffId && rc.ClientId == clientId && rc.ProjectId == projectId
                        && rc.EffectiveFrom <= asOfDate)
            .OrderByDescending(rc => rc.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<RateCard>> ListAsync(int? staffId, int? roleId, int? clientId, int? projectId, CancellationToken ct)
    {
        var query = db.RateCards
            .Include(rc => rc.Role)
            .Include(rc => rc.Staff)
            .Include(rc => rc.Client)
            .Include(rc => rc.Project)
            .AsQueryable();

        if (staffId is not null) query = query.Where(rc => rc.StaffId == staffId);
        if (roleId is not null) query = query.Where(rc => rc.RoleId == roleId);
        if (clientId is not null) query = query.Where(rc => rc.ClientId == clientId);
        if (projectId is not null) query = query.Where(rc => rc.ProjectId == projectId);

        var rows = await query.ToListAsync(ct);
        return rows.OrderByDescending(rc => rc.EffectiveFrom).ToList();
    }

    public async Task AddAsync(RateCard rateCard, CancellationToken ct) => await db.RateCards.AddAsync(rateCard, ct);
}
