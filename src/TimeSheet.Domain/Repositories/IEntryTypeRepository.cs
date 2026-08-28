using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IEntryTypeRepository
{
    Task<EntryType?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<EntryType>> GetByProjectIdAsync(int projectId, bool includeInactive, CancellationToken ct);
    Task AddAsync(EntryType entryType, CancellationToken ct);
    void Update(EntryType entryType);
}
