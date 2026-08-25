using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class NotificationRepository(TimesheetDbContext db) : INotificationRepository
{
    public Task<Notification?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<IReadOnlyList<Notification>> GetUnreadAsync(int userId, CancellationToken ct) =>
        await db.Notifications
            .Where(n => n.RecipientUserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task AddAsync(Notification notification, CancellationToken ct) => await db.Notifications.AddAsync(notification, ct);

    public void Update(Notification notification) => db.Notifications.Update(notification);

    public async Task MarkAllReadAsync(int userId, CancellationToken ct)
    {
        var unread = await db.Notifications.Where(n => n.RecipientUserId == userId && !n.IsRead).ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAtUtc = now;
        }
    }
}
