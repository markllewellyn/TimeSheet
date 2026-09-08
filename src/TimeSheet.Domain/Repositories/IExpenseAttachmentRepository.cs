using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IExpenseAttachmentRepository
{
    Task<ExpenseAttachment?> GetByIdAsync(int id, CancellationToken ct);
    Task AddAsync(ExpenseAttachment attachment, CancellationToken ct);
    void Remove(ExpenseAttachment attachment);
}
