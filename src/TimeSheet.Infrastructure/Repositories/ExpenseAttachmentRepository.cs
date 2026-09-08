using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class ExpenseAttachmentRepository(TimesheetDbContext db) : IExpenseAttachmentRepository
{
    public Task<ExpenseAttachment?> GetByIdAsync(int id, CancellationToken ct) =>
        db.ExpenseAttachments.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task AddAsync(ExpenseAttachment attachment, CancellationToken ct) => await db.ExpenseAttachments.AddAsync(attachment, ct);

    public void Remove(ExpenseAttachment attachment) => db.ExpenseAttachments.Remove(attachment);
}
