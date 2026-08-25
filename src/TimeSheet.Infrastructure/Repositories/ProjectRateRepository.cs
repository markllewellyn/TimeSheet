using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class ProjectRateRepository(TimesheetDbContext db) : IProjectRateRepository
{
    public async Task<ProjectRate?> GetEffectiveRateAsync(int projectId, int? userId, DateOnly onDate, CancellationToken ct)
    {
        // Most-specific-first: a per-user row covering the date, else the project-default (UserId null) row.
        if (userId is not null)
        {
            var userRate = await db.ProjectRates
                .Where(r => r.ProjectId == projectId && r.UserId == userId
                            && r.EffectiveFrom <= onDate && (r.EffectiveTo == null || r.EffectiveTo >= onDate))
                .OrderByDescending(r => r.EffectiveFrom)
                .FirstOrDefaultAsync(ct);
            if (userRate is not null) return userRate;
        }

        return await db.ProjectRates
            .Where(r => r.ProjectId == projectId && r.UserId == null
                        && r.EffectiveFrom <= onDate && (r.EffectiveTo == null || r.EffectiveTo >= onDate))
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ProjectRate>> GetHistoryAsync(int projectId, int? userId, CancellationToken ct) =>
        await db.ProjectRates
            .Where(r => r.ProjectId == projectId && r.UserId == userId)
            .OrderByDescending(r => r.EffectiveFrom)
            .ToListAsync(ct);

    public async Task<bool> HasOverlapAsync(int projectId, int? userId, DateOnly from, DateOnly? to, int? excludeId, CancellationToken ct) =>
        await db.ProjectRates.AnyAsync(r =>
            r.ProjectId == projectId && r.UserId == userId && (excludeId == null || r.Id != excludeId)
            && r.EffectiveFrom <= (to ?? DateOnly.MaxValue)
            && (r.EffectiveTo == null || r.EffectiveTo >= from), ct);

    public async Task AddAsync(ProjectRate rate, CancellationToken ct) => await db.ProjectRates.AddAsync(rate, ct);

    public void Update(ProjectRate rate) => db.ProjectRates.Update(rate);
}
