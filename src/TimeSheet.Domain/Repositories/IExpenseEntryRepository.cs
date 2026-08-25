using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IExpenseEntryRepository
{
    Task<ExpenseEntry?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<ExpenseEntry>> GetForUserAsync(int userId, string? searchText, DateOnly? from, DateOnly? to, CancellationToken ct);
    Task AddAsync(ExpenseEntry entry, CancellationToken ct);
    void Update(ExpenseEntry entry);
    void Remove(ExpenseEntry entry);
}
