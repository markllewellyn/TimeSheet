using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>
/// In-app toasts use polling, not SignalR/push, for v1 - a serverless SPA + Functions API has no persistent
/// connection by default, and Azure SignalR Service's extra Azure resource + connection-lifecycle handling
/// isn't justified for a handful of internal users where a toast arriving 60-120s late is a non-issue. The
/// Angular app polls GET /notifications/unread on an interval, on tab focus, and immediately after login.
/// </summary>
public class NotificationsFunctions(INotificationRepository notifications, IUnitOfWork uow, ICurrentUserAccessor currentUser)
{
    [Function("Notifications_Unread")]
    public async Task<IActionResult> Unread(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notifications/unread")] HttpRequest req, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var result = await notifications.GetUnreadAsync(user.UserId, ct);
        return new OkObjectResult(result.Select(ToDto));
    }

    [Function("Notifications_MarkRead")]
    public async Task<IActionResult> MarkRead(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "notifications/{id:int}/read")] HttpRequest req, int id, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var notification = await notifications.GetByIdAsync(id, ct);
        if (notification is null || notification.RecipientUserId != user.UserId) return new NotFoundResult();

        notification.IsRead = true;
        notification.ReadAtUtc = DateTimeOffset.UtcNow;
        notifications.Update(notification);
        await uow.SaveChangesAsync(ct);
        return new NoContentResult();
    }

    [Function("Notifications_MarkAllRead")]
    public async Task<IActionResult> MarkAllRead(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "notifications/read-all")] HttpRequest req, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        await notifications.MarkAllReadAsync(user.UserId, ct);
        await uow.SaveChangesAsync(ct);
        return new NoContentResult();
    }

    private static object ToDto(Notification n) => new
    {
        n.Id,
        Type = n.Type.ToString(),
        n.Message,
        n.IsRead,
        n.CreatedAtUtc,
        n.RelatedProjectId,
        n.RelatedTimesheetEntryId,
    };
}
