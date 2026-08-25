using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>Resolves the caller's app User from the Entra `oid` claim on every authenticated request.</summary>
    Task<User?> GetByEntraObjectIdAsync(string entraObjectId, CancellationToken ct);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<IReadOnlyList<User>> GetAllAsync(bool includeInactive, CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
    void Update(User user);
}
