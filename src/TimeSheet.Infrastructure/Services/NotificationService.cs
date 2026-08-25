using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class NotificationService(INotificationRepository notifications, IUserRepository users, IEmailSender emailSender, IUnitOfWork uow) : INotificationService
{
    public async Task RaiseAsync(
        int recipientUserId, NotificationType type, string message, NotificationChannel channel,
        int? relatedProjectId, int? relatedTimesheetEntryId, CancellationToken ct)
    {
        var notification = new Notification
        {
            RecipientUserId = recipientUserId,
            Type = type,
            Message = message,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            RelatedProjectId = relatedProjectId,
            RelatedTimesheetEntryId = relatedTimesheetEntryId,
        };
        await notifications.AddAsync(notification, ct);
        await uow.SaveChangesAsync(ct);

        if (channel is NotificationChannel.EmailOnly or NotificationChannel.Both)
        {
            var user = await users.GetByIdAsync(recipientUserId, ct);
            if (user is not null)
            {
                await emailSender.SendAsync(user.Email, TitleFor(type), $"<p>{message}</p>", ct);
            }
        }
    }

    public async Task RaiseToAdminsAsync(
        NotificationType type, string message, NotificationChannel channel,
        int? relatedProjectId, int? relatedTimesheetEntryId, CancellationToken ct)
    {
        var admins = (await users.GetAllAsync(includeInactive: false, ct)).Where(u => u.Role == UserRole.Admin);
        foreach (var admin in admins)
        {
            await RaiseAsync(admin.Id, type, message, channel, relatedProjectId, relatedTimesheetEntryId, ct);
        }
    }

    private static string TitleFor(NotificationType type) => type switch
    {
        NotificationType.TimesheetReminder => "Timesheet reminder",
        NotificationType.BudgetWarning => "Project budget warning",
        NotificationType.BudgetExceeded => "Project budget exceeded",
        NotificationType.EscalationRaised => "Budget approval required",
        NotificationType.EscalationDecided => "Timesheet entry decision",
        NotificationType.InvoiceGenerated => "Invoice generated",
        NotificationType.ProjectHealthDeclined => "Project health alert",
        _ => "Notification",
    };
}
