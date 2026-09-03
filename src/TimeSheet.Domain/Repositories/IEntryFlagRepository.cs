using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IEntryFlagRepository
{
    Task<EntryFlag?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<EntryFlag>> GetOpenAsync(CancellationToken ct);

    /// <summary>Every open (not-yet-cleared) flag against any of the given TimesheetEntry ids - lets a list of
    /// entries show what each one's flag is actually for (reason/notes), without an N+1 lookup per row. A flag
    /// is never a gate (FDD), so this is purely informational. An entry can in principle carry more than one
    /// open flag (e.g. a system-raised budget flag alongside a manual one) - callers should group by
    /// TimesheetEntryId themselves rather than assume at most one.</summary>
    Task<IReadOnlyList<EntryFlag>> GetOpenByTimesheetEntryIdsAsync(IReadOnlyCollection<int> timesheetEntryIds, CancellationToken ct);

    Task AddAsync(EntryFlag flag, CancellationToken ct);
    void Update(EntryFlag flag);
}
