using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class TeamRepository(TimesheetDbContext db) : ITeamRepository
{
    public Task<Team?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Teams.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Team?> GetByNameAsync(string name, CancellationToken ct) =>
        db.Teams.FirstOrDefaultAsync(t => t.Name == name, ct);

    public async Task<IReadOnlyList<Team>> GetAllAsync(bool includeInactive, CancellationToken ct)
    {
        var query = db.Teams.AsQueryable();
        if (!includeInactive) query = query.Where(t => t.IsActive);
        return await query.OrderBy(t => t.Name).ToListAsync(ct);
    }

    public async Task AddAsync(Team team, CancellationToken ct) => await db.Teams.AddAsync(team, ct);

    public void Update(Team team) => db.Teams.Update(team);
}
