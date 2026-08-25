using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IExpenseEntryRepository
{
    Task<ExpenseEntry?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<ExpenseEntry>> GetForUserAsync(int userId, string? searchText, DateOnly? from, DateOnly? to, CancellationToken ct);

    /// <summary>Billable expenses for a project within a date range, across all users - used by invoicing.</summary>
    Task<IReadOnlyList<ExpenseEntry>> GetBillableForProjectAsync(int projectId, DateOnly from, DateOnly to, CancellationToken ct);

    Task AddAsync(ExpenseEntry entry, CancellationToken ct);
    void Update(ExpenseEntry entry);
    void Remove(ExpenseEntry entry);
}
