using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class EscalationRepository(TimesheetDbContext db) : IEscalationRepository
{
    public Task<Escalation?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Escalations.Include(e => e.TimesheetEntry).Include(e => e.Project).ThenInclude(p => p!.Client)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<Escalation>> GetPendingAsync(CancellationToken ct)
    {
        // SQLite can't translate ORDER BY on a DateTimeOffset column - order client-side instead (result sets
        // here are small: currently-pending escalations only).
        var escalations = await db.Escalations
            .Include(e => e.TimesheetEntry).ThenInclude(te => te!.User)
            .Include(e => e.Project).ThenInclude(p => p!.Client)
            .Where(e => e.Decision == EscalationDecision.Pending)
            .ToListAsync(ct);
        return escalations.OrderBy(e => e.RaisedAtUtc).ToList();
    }

    public async Task AddAsync(Escalation escalation, CancellationToken ct) => await db.Escalations.AddAsync(escalation, ct);

    public void Update(Escalation escalation) => db.Escalations.Update(escalation);
}
