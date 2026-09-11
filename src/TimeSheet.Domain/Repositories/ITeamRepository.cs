using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(int id, CancellationToken ct);
    Task<Team?> GetByNameAsync(string name, CancellationToken ct);
    Task<IReadOnlyList<Team>> GetAllAsync(bool includeInactive, CancellationToken ct);
    Task AddAsync(Team team, CancellationToken ct);
    void Update(Team team);
}
