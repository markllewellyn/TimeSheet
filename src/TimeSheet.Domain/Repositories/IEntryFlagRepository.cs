using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IEntryFlagRepository
{
    Task<EntryFlag?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<EntryFlag>> GetOpenAsync(CancellationToken ct);
    Task AddAsync(EntryFlag flag, CancellationToken ct);
    void Update(EntryFlag flag);
}
