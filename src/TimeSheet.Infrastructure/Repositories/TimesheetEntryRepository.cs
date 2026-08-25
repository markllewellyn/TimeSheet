using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class TimesheetEntryRepository(TimesheetDbContext db) : ITimesheetEntryRepository
{
    public async Task<decimal> GetTotalCountedHoursForProjectAsync(int projectId, CancellationToken ct) =>
        await db.TimesheetEntries
            .Where(e => e.ProjectId == projectId && (e.Status == TimesheetEntryStatus.Normal || e.Status == TimesheetEntryStatus.Approved))
            .SumAsync(e => e.WorkHours + e.OutOfHoursHours, ct);

    public Task<TimesheetEntry?> GetByIdAsync(int id, CancellationToken ct) =>
        db.TimesheetEntries.Include(e => e.Attachments).Include(e => e.Project).ThenInclude(p => p!.Client)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<TimesheetEntry>> GetForUserAsync(int userId, string? searchText, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var query = db.TimesheetEntries
            .Include(e => e.Project).ThenInclude(p => p!.Client)
            .Include(e => e.Attachments)
            .Where(e => e.UserId == userId);

        if (from is not null) query = query.Where(e => e.Date >= from);
        if (to is not null) query = query.Where(e => e.Date <= to);
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim();
            query = query.Where(e =>
                EF.Functions.Like(e.Project!.Client!.Name, $"%{term}%") ||
                EF.Functions.Like(e.Project!.Client!.AccountCode, $"%{term}%") ||
                EF.Functions.Like(e.Project!.Name, $"%{term}%"));
        }

        return await query.OrderByDescending(e => e.Date).ToListAsync(ct);
    }

    public async Task AddAsync(TimesheetEntry entry, CancellationToken ct) => await db.TimesheetEntries.AddAsync(entry, ct);

    public void Update(TimesheetEntry entry) => db.TimesheetEntries.Update(entry);

    public void Remove(TimesheetEntry entry) => db.TimesheetEntries.Remove(entry);
}
