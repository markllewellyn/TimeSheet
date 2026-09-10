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

    public async Task<IReadOnlyList<TimesheetEntry>> GetAllCountedForProjectAsync(int projectId, CancellationToken ct) =>
        await db.TimesheetEntries
            .Include(e => e.User)
            .Include(e => e.EntryType)
            .Where(e => e.ProjectId == projectId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TimesheetEntry>> GetCountedForInvoicingAsync(int projectId, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct)
    {
        var periodLengthDays = periodEnd.DayNumber - periodStart.DayNumber + 1;
        var previousPeriodEnd = periodStart.AddDays(-1);
        var previousPeriodStart = previousPeriodEnd.AddDays(-(periodLengthDays - 1));

        return await db.TimesheetEntries
            .Include(e => e.User)
            // InvoiceId == null - an entry already locked to a prior invoice must never be counted again on a
            // new one (BuildDraftAsync and LockEntriesAsync both call this query) - without this, regenerating
            // or generating a second overlapping-period draft would double-bill the client for the same hours.
            .Where(e => e.ProjectId == projectId && e.InvoiceId == null &&
                ((e.Date >= periodStart && e.Date <= periodEnd && e.BillingPeriodChoice == BillingPeriodChoice.Current) ||
                 (e.Date >= previousPeriodStart && e.Date <= previousPeriodEnd && e.BillingPeriodChoice == BillingPeriodChoice.Next)))
            .ToListAsync(ct);
    }

    public async Task<decimal> GetTotalCountedHoursForProjectAsync(int projectId, CancellationToken ct) =>
        await db.TimesheetEntries
            .Where(e => e.ProjectId == projectId)
            .SumAsync(e => e.WorkHours + e.OutOfHoursHours, ct);

    public async Task<IReadOnlyDictionary<int, ProjectActuals>> GetActualsByProjectIdsAsync(IReadOnlyCollection<int> projectIds, CancellationToken ct) =>
        (await db.TimesheetEntries
            .Where(e => projectIds.Contains(e.ProjectId))
            .GroupBy(e => e.ProjectId)
            .Select(g => new
            {
                ProjectId = g.Key,
                TotalHours = g.Sum(e => e.WorkHours + e.OutOfHoursHours),
                TotalCost = g.Sum(e => e.WorkHours * (e.ResolvedHourlyCost ?? 0) + e.OutOfHoursHours * (e.ResolvedOutOfHoursCost ?? 0)),
            })
            .ToListAsync(ct))
            .ToDictionary(x => x.ProjectId, x => new ProjectActuals(x.TotalHours, x.TotalCost));

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
            query = ApplyClientProjectSearch(query, searchText.Trim());
        }

        return await query.OrderByDescending(e => e.Date).ToListAsync(ct);
    }

    /// <summary>Requires each whitespace-separated word in the search term to match somewhere across client
    /// name/account code or project name - independently, not as one literal phrase - so e.g. "Everlast
    /// migration" finds an entry even when "Everlast" is the client and "migration" only appears in the
    /// project name.</summary>
    private static IQueryable<TimesheetEntry> ApplyClientProjectSearch(IQueryable<TimesheetEntry> query, string term)
    {
        foreach (var word in term.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            query = query.Where(e =>
                EF.Functions.Like(e.Project!.Client!.Name, $"%{word}%") ||
                EF.Functions.Like(e.Project!.Client!.AccountCode, $"%{word}%") ||
                EF.Functions.Like(e.Project!.Name, $"%{word}%"));
        }
        return query;
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
            query = ApplyStaffClientProjectSearch(query, searchText.Trim());
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
        query = int.TryParse(term, out var entryId)
            ? query.Where(e => e.Id == entryId)
            : ApplyStaffClientProjectSearch(query, term);

        return await query.OrderByDescending(e => e.Date).Take(take).ToListAsync(ct);
    }

    /// <summary>Requires each whitespace-separated word in the search term to match somewhere across staff/
    /// client/project name or the entry's own description - independently, not as one literal phrase - so
    /// e.g. "Sarah migration" finds an entry even when "Sarah" is the staff name and "migration" only appears
    /// in the project name, and "mark ltd fast" finds one where "fast" only appears in its description.</summary>
    private static IQueryable<TimesheetEntry> ApplyStaffClientProjectSearch(IQueryable<TimesheetEntry> query, string term)
    {
        foreach (var word in term.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            query = query.Where(e =>
                EF.Functions.Like(e.User!.DisplayName, $"%{word}%") ||
                EF.Functions.Like(e.Client!.Name, $"%{word}%") ||
                EF.Functions.Like(e.Project!.Name, $"%{word}%") ||
                EF.Functions.Like(e.Description, $"%{word}%"));
        }
        return query;
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

    public async Task<IReadOnlyList<TimesheetEntry>> GetByInvoiceIdAsync(int invoiceId, CancellationToken ct) =>
        await db.TimesheetEntries.Where(e => e.InvoiceId == invoiceId).ToListAsync(ct);

    public async Task AddAsync(TimesheetEntry entry, CancellationToken ct) => await db.TimesheetEntries.AddAsync(entry, ct);

    public void Update(TimesheetEntry entry) => db.TimesheetEntries.Update(entry);

    public void Remove(TimesheetEntry entry) => db.TimesheetEntries.Remove(entry);
}
