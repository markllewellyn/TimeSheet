using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<Notification>> GetUnreadAsync(int userId, CancellationToken ct);
    Task AddAsync(Notification notification, CancellationToken ct);
    void Update(Notification notification);
    Task MarkAllReadAsync(int userId, CancellationToken ct);
}
