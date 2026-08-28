using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class EntryTypeRepository(TimesheetDbContext db) : IEntryTypeRepository
{
    public Task<EntryType?> GetByIdAsync(int id, CancellationToken ct) =>
        db.EntryTypes.Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<EntryType>> GetByProjectIdAsync(int projectId, bool includeInactive, CancellationToken ct)
    {
        var query = db.EntryTypes.Where(t => t.ProjectId == projectId);
        if (!includeInactive) query = query.Where(t => t.IsActive);
        return await query.OrderBy(t => t.Name).ToListAsync(ct);
    }

    public async Task AddAsync(EntryType entryType, CancellationToken ct) => await db.EntryTypes.AddAsync(entryType, ct);

    public void Update(EntryType entryType) => db.EntryTypes.Update(entryType);
}
