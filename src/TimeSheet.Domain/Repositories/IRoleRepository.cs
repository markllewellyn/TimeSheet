using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(int id, CancellationToken ct);
    Task<Role?> GetByNameAsync(string name, CancellationToken ct);
    Task<IReadOnlyList<Role>> GetAllAsync(bool includeInactive, CancellationToken ct);
    Task AddAsync(Role role, CancellationToken ct);
    void Update(Role role);
}
