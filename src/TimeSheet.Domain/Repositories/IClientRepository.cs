using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<Client>> GetAllAsync(bool includeInactive, CancellationToken ct);
    Task<bool> AccountCodeExistsAsync(string accountCode, int? excludeId, CancellationToken ct);
    Task AddAsync(Client client, CancellationToken ct);
    void Update(Client client);
}
