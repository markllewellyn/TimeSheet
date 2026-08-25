namespace TimeSheet.Domain.Services;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct);
}

/// <summary>
/// Always writes the Notification row first (the in-app polling endpoint's source of truth); an email failure
/// is logged and swallowed, never rolled back against the in-app notification - email is best-effort, in-app
/// is authoritative.
/// </summary>
public interface INotificationService
{
    Task RaiseAsync(
        int recipientUserId,
        NotificationType type,
        string message,
        NotificationChannel channel = NotificationChannel.Both,
        int? relatedProjectId = null,
        int? relatedTimesheetEntryId = null,
        CancellationToken ct = default);

    /// <summary>Convenience for the common "notify every Admin" case (escalations, invoice generation, project
    /// health alerts) - resolves the current Admin roster and raises one notification per Admin.</summary>
    Task RaiseToAdminsAsync(
        NotificationType type,
        string message,
        NotificationChannel channel = NotificationChannel.Both,
        int? relatedProjectId = null,
        int? relatedTimesheetEntryId = null,
        CancellationToken ct = default);
}
