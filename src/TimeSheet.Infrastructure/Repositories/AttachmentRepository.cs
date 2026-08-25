using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class AttachmentRepository(TimesheetDbContext db) : IAttachmentRepository
{
    public Task<Attachment?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Attachments.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Attachment>> GetByTimesheetEntryAsync(int timesheetEntryId, CancellationToken ct) =>
        await db.Attachments.Where(a => a.TimesheetEntryId == timesheetEntryId).ToListAsync(ct);

    public async Task AddAsync(Attachment attachment, CancellationToken ct) => await db.Attachments.AddAsync(attachment, ct);

    public void Remove(Attachment attachment) => db.Attachments.Remove(attachment);
}
