using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class AuditLogRepository(TimesheetDbContext db) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog log, CancellationToken ct) => await db.AuditLogs.AddAsync(log, ct);

    public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(int? userId, DateOnly? from, DateOnly? to, int take, CancellationToken ct)
    {
        // userId narrows server-side; date range and ordering are done client-side, same as every other
        // DateTimeOffset-ordered query in this codebase - SQLite's EF provider can't translate ORDER BY on it.
        var query = db.AuditLogs.AsQueryable();
        if (userId is not null) query = query.Where(a => a.UserId == userId || a.ImpersonatedUserId == userId);

        var logs = await query.ToListAsync(ct);
        var filtered = logs.Where(a =>
            (from is null || DateOnly.FromDateTime(a.CreatedUtc.Date) >= from) &&
            (to is null || DateOnly.FromDateTime(a.CreatedUtc.Date) <= to));

        return filtered.OrderByDescending(a => a.CreatedUtc).Take(take).ToList();
    }
}
