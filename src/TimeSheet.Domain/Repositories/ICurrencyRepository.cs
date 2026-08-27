using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface ICurrencyRepository
{
    Task<Currency?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<Currency>> GetAllAsync(CancellationToken ct);
}
