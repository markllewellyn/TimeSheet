using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class TimesheetEntryRepository(TimesheetDbContext db) : ITimesheetEntryRepository
{
    public async Task<IReadOnlyList<TimesheetEntry>> GetCountedForProjectAsync(int projectId, DateOnly from, DateOnly to, CancellationToken ct) =>
        await db.TimesheetEntries
            .Include(e => e.User)
            .Where(e => e.ProjectId == projectId && e.Date >= from && e.Date <= to)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TimesheetEntry>> GetCountedForInvoicingAsync(int projectId, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct)
    {
        var periodLengthDays = periodEnd.DayNumber - periodStart.DayNumber + 1;
        var previousPeriodEnd = periodStart.AddDays(-1);
        var previousPeriodStart = previousPeriodEnd.AddDays(-(periodLengthDays - 1));

        return await db.TimesheetEntries
            .Include(e => e.User)
            .Where(e => e.ProjectId == projectId &&
                ((e.Date >= periodStart && e.Date <= periodEnd && e.BillingPeriodChoice == BillingPeriodChoice.Current) ||
                 (e.Date >= previousPeriodStart && e.Date <= previousPeriodEnd && e.BillingPeriodChoice == BillingPeriodChoice.Next)))
            .ToListAsync(ct);
    }

    public async Task<decimal> GetTotalCountedHoursForProjectAsync(int projectId, CancellationToken ct) =>
        await db.TimesheetEntries
            .Where(e => e.ProjectId == projectId)
            .SumAsync(e => e.WorkHours + e.OutOfHoursHours, ct);

    public async Task<decimal> GetTotalHoursForUserDateAsync(int userId, DateOnly date, int? excludeEntryId, CancellationToken ct) =>
        await db.TimesheetEntries
            .Where(e => e.UserId == userId && e.Date == date && (excludeEntryId == null || e.Id != excludeEntryId.Value))
            .SumAsync(e => e.WorkHours + e.OutOfHoursHours, ct);

    public Task<TimesheetEntry?> GetByIdAsync(int id, CancellationToken ct) =>
        db.TimesheetEntries.Include(e => e.Attachments).Include(e => e.Client).Include(e => e.Project).Include(e => e.EntryType)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<TimesheetEntry>> GetForUserAsync(int userId, string? searchText, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var query = db.TimesheetEntries
            .Include(e => e.Client)
            .Include(e => e.Project)
            .Include(e => e.Attachments)
            .Include(e => e.EntryType)
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

    public async Task<IReadOnlyList<TimesheetEntry>> GetAllInRangeAsync(
        DateOnly? from, DateOnly? to, int? clientId, int? projectId, IReadOnlyCollection<int>? projectIds, int? userId, CancellationToken ct)
    {
        var query = db.TimesheetEntries.Include(e => e.User).Include(e => e.Client).Include(e => e.Project).AsQueryable();
        if (from is not null) query = query.Where(e => e.Date >= from);
        if (to is not null) query = query.Where(e => e.Date <= to);
        if (clientId is not null) query = query.Where(e => e.ClientId == clientId);
        if (projectId is not null) query = query.Where(e => e.ProjectId == projectId);
        if (projectIds is not null) query = query.Where(e => projectIds.Contains(e.ProjectId));
        if (userId is not null) query = query.Where(e => e.UserId == userId);
        return await query.OrderBy(e => e.Date).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TimesheetEntry>> GetPendingApprovalAsync(string? searchText, IReadOnlyCollection<int>? projectIds, CancellationToken ct)
    {
        var query = db.TimesheetEntries
            .Include(e => e.User)
            .Include(e => e.Client)
            .Include(e => e.Project)
            .Where(e => !e.ApprovedPayroll);

        if (projectIds is not null) query = query.Where(e => projectIds.Contains(e.ProjectId));

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim();
            query = query.Where(e =>
                EF.Functions.Like(e.User!.DisplayName, $"%{term}%") ||
                EF.Functions.Like(e.Client!.Name, $"%{term}%") ||
                EF.Functions.Like(e.Project!.Name, $"%{term}%"));
        }

        return await query.OrderBy(e => e.Date).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TimesheetEntry>> SearchForFlaggingAsync(string searchText, IReadOnlyCollection<int>? projectIds, int take, CancellationToken ct)
    {
        var query = db.TimesheetEntries
            .Include(e => e.User)
            .Include(e => e.Client)
            .Include(e => e.Project)
            .AsQueryable();

        if (projectIds is not null) query = query.Where(e => projectIds.Contains(e.ProjectId));

        var term = searchText.Trim();
        var isEntryId = int.TryParse(term, out var entryId);
        query = query.Where(e =>
            (isEntryId && e.Id == entryId) ||
            EF.Functions.Like(e.User!.DisplayName, $"%{term}%") ||
            EF.Functions.Like(e.Client!.Name, $"%{term}%") ||
            EF.Functions.Like(e.Project!.Name, $"%{term}%"));

        return await query.OrderByDescending(e => e.Date).Take(take).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TimesheetEntry>> GetReadyForPayrollAsync(IReadOnlyCollection<int>? projectIds, CancellationToken ct)
    {
        var query = db.TimesheetEntries
            .Include(e => e.User)
            .Include(e => e.Client)
            .Include(e => e.Project)
            .Where(e => e.ApprovedPayroll && !e.SentToPayroll);

        if (projectIds is not null) query = query.Where(e => projectIds.Contains(e.ProjectId));

        return await query.OrderBy(e => e.Date).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TimesheetEntry>> GetOutOfHoursApprovedNotSentAsync(DateOnly periodStart, DateOnly periodEnd, CancellationToken ct) =>
        await db.TimesheetEntries
            .Include(e => e.User)
            .Where(e => e.OutOfHoursHours > 0 && e.ApprovedPayroll && !e.SentToPayroll && e.Date >= periodStart && e.Date <= periodEnd)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> GetDistinctPostingBatchesAsync(CancellationToken ct) =>
        await db.TimesheetEntries
            .Where(e => e.PostingBatch != null && e.PostingBatch != "")
            .Select(e => e.PostingBatch!)
            .Distinct()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TimesheetEntry>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        await db.TimesheetEntries.Include(e => e.Project).Where(e => ids.Contains(e.Id)).ToListAsync(ct);

    public async Task AddAsync(TimesheetEntry entry, CancellationToken ct) => await db.TimesheetEntries.AddAsync(entry, ct);

    public void Update(TimesheetEntry entry) => db.TimesheetEntries.Update(entry);

    public void Remove(TimesheetEntry entry) => db.TimesheetEntries.Remove(entry);
}
