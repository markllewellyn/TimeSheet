using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class ExpenseEntryRepository(TimesheetDbContext db) : IExpenseEntryRepository
{
    public Task<ExpenseEntry?> GetByIdAsync(int id, CancellationToken ct) =>
        db.ExpenseEntries.Include(e => e.Project).ThenInclude(p => p!.Client).FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<ExpenseEntry>> GetForUserAsync(int userId, string? searchText, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var query = db.ExpenseEntries
            .Include(e => e.Project).ThenInclude(p => p!.Client)
            .Where(e => e.UserId == userId);

        if (from is not null) query = query.Where(e => e.Date >= from);
        if (to is not null) query = query.Where(e => e.Date <= to);
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            foreach (var word in searchText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                query = query.Where(e =>
                    EF.Functions.Like(e.Project!.Client!.Name, $"%{word}%") ||
                    EF.Functions.Like(e.Project!.Client!.AccountCode, $"%{word}%") ||
                    EF.Functions.Like(e.Project!.Name, $"%{word}%"));
            }
        }

        return await query.OrderByDescending(e => e.Date).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ExpenseEntry>> GetBillableForProjectAsync(int projectId, DateOnly from, DateOnly to, CancellationToken ct) =>
        await db.ExpenseEntries
            .Where(e => e.ProjectId == projectId && e.Date >= from && e.Date <= to && e.IsBillable)
            .ToListAsync(ct);

    public async Task AddAsync(ExpenseEntry entry, CancellationToken ct) => await db.ExpenseEntries.AddAsync(entry, ct);

    public void Update(ExpenseEntry entry) => db.ExpenseEntries.Update(entry);

    public void Remove(ExpenseEntry entry) => db.ExpenseEntries.Remove(entry);
}
