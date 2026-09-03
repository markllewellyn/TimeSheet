using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class EntryFlagRepository(TimesheetDbContext db) : IEntryFlagRepository
{
    public Task<EntryFlag?> GetByIdAsync(int id, CancellationToken ct) =>
        db.EntryFlags.Include(f => f.TimesheetEntry).Include(f => f.Project).ThenInclude(p => p!.Client)
            .FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<IReadOnlyList<EntryFlag>> GetOpenAsync(CancellationToken ct)
    {
        // SQLite can't translate ORDER BY on a DateTimeOffset column - order client-side instead (result sets
        // here are small: currently-open flags only).
        var flags = await db.EntryFlags
            .Include(f => f.TimesheetEntry).ThenInclude(te => te!.User)
            .Include(f => f.Project).ThenInclude(p => p!.Client)
            .Where(f => !f.IsCleared)
            .ToListAsync(ct);
        return flags.OrderBy(f => f.RaisedAtUtc).ToList();
    }

    public async Task<IReadOnlyList<EntryFlag>> GetOpenByTimesheetEntryIdsAsync(IReadOnlyCollection<int> timesheetEntryIds, CancellationToken ct)
    {
        // SQLite can't translate ORDER BY on a DateTimeOffset column - order client-side instead, same as
        // GetOpenAsync above.
        var flags = await db.EntryFlags
            .Where(f => !f.IsCleared && timesheetEntryIds.Contains(f.TimesheetEntryId))
            .ToListAsync(ct);
        return flags.OrderByDescending(f => f.RaisedAtUtc).ToList();
    }

    public async Task AddAsync(EntryFlag flag, CancellationToken ct) => await db.EntryFlags.AddAsync(flag, ct);

    public void Update(EntryFlag flag) => db.EntryFlags.Update(flag);
}
