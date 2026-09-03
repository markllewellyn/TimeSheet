using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class ProjectAttachmentRepository(TimesheetDbContext db) : IProjectAttachmentRepository
{
    public Task<ProjectAttachment?> GetByIdAsync(int id, CancellationToken ct) =>
        db.ProjectAttachments.Include(a => a.UploadedBy).FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<ProjectAttachment>> GetByProjectAsync(int projectId, CancellationToken ct)
    {
        // SQLite can't translate ORDER BY on a DateTimeOffset column - order client-side instead, same as
        // every other DateTimeOffset-ordered query in this codebase (see NotificationRepository/AuditLogRepository).
        var attachments = await db.ProjectAttachments.Include(a => a.UploadedBy).Where(a => a.ProjectId == projectId).ToListAsync(ct);
        return attachments.OrderByDescending(a => a.UploadedAtUtc).ToList();
    }

    public async Task AddAsync(ProjectAttachment attachment, CancellationToken ct) => await db.ProjectAttachments.AddAsync(attachment, ct);

    public void Remove(ProjectAttachment attachment) => db.ProjectAttachments.Remove(attachment);
}
