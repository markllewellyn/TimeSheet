using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>The two-stage payroll workflow mirrored from the legacy Power App's Admin Overview screen: approve
/// raw timesheet entries (grouping them into a named Posting Batch), then separately mark the approved batch as
/// sent to payroll. Distinct from the budget-overrun EntryFlags queue - this approves the entry itself, not a
/// budget exception. Admin sees/acts on every entry; a project manager is additionally scoped to entries on
/// their own managed project(s) only (FDD: "out of hours work must be approved by project managers or
/// administrators") - see AuthorizationExtensions.RequireAdminOrProjectManager for the equivalent pattern used
/// elsewhere; this class inlines the same idea since it's list-scoping, not a single-entity gate.</summary>
public class TimesheetApprovalFunctions(
    ITimesheetEntryRepository entries,
    IProjectRepository projects,
    IAuditLogService auditLog,
    IUnitOfWork uow,
    ICurrentUserAccessor currentUser)
{
    [Function("Approvals_Pending")]
    public async Task<IActionResult> Pending(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "approvals/pending")] HttpRequest req, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var (managedProjectIds, hasNoAccess) = await ResolveScopeAsync(user, ct);
        if (hasNoAccess) return new OkObjectResult(new PendingApprovalsResponse([], []));

        var search = req.Query["search"].ToString();
        var pending = await entries.GetPendingApprovalAsync(string.IsNullOrWhiteSpace(search) ? null : search, managedProjectIds, ct);
        var batches = await entries.GetDistinctPostingBatchesAsync(ct);

        return new OkObjectResult(new PendingApprovalsResponse(pending.Select(ToDto).ToList(), batches));
    }

    [Function("Approvals_Approve")]
    public async Task<IActionResult> Approve(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "approvals/approve")] HttpRequest req, CancellationToken ct)
    {
        var admin = currentUser.RequireUser();

        var body = await req.ReadFromJsonAsync<ApproveEntriesRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        if (body.EntryIds is null or { Length: 0 })
        {
            return new BadRequestObjectResult(new { error = "At least one entry must be selected." });
        }

        if (string.IsNullOrWhiteSpace(body.PostingBatch))
        {
            return new BadRequestObjectResult(new { error = "A posting batch is required." });
        }

        var toApprove = await entries.GetByIdsAsync(body.EntryIds, ct);
        if (await CheckOutOfScopeAsync(admin, toApprove, ct) is { } outOfScope) return outOfScope;

        var now = DateTimeOffset.UtcNow;
        var approved = 0;
        foreach (var entry in toApprove.Where(e => !e.ApprovedPayroll))
        {
            entry.ApprovedPayroll = true;
            entry.ApprovedByStaffId = admin.UserId;
            entry.ApprovedByName = admin.DisplayName;
            entry.DateApprovedPayroll = now;
            entry.PostingBatch = body.PostingBatch;
            entries.Update(entry);
            await auditLog.LogAsync(admin, "TimesheetEntry.ApprovedForPayroll", "TimesheetEntry", entry.Id,
                $"batch '{body.PostingBatch}'", impersonatedUserId: null, ct);
            approved++;
        }

        await uow.SaveChangesAsync(ct);
        return new OkObjectResult(new { approved });
    }

    [Function("Approvals_ReadyForPayroll")]
    public async Task<IActionResult> ReadyForPayroll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "approvals/ready-for-payroll")] HttpRequest req, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var (managedProjectIds, hasNoAccess) = await ResolveScopeAsync(user, ct);
        if (hasNoAccess) return new OkObjectResult(Array.Empty<ApprovalEntryDto>());

        var ready = await entries.GetReadyForPayrollAsync(managedProjectIds, ct);
        return new OkObjectResult(ready.Select(ToDto));
    }

    [Function("Approvals_SendToPayroll")]
    public async Task<IActionResult> SendToPayroll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "approvals/send-to-payroll")] HttpRequest req, CancellationToken ct)
    {
        var admin = currentUser.RequireUser();

        var body = await req.ReadFromJsonAsync<SendToPayrollRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        if (body.EntryIds is null or { Length: 0 })
        {
            return new BadRequestObjectResult(new { error = "At least one entry must be selected." });
        }

        var toSend = await entries.GetByIdsAsync(body.EntryIds, ct);
        if (await CheckOutOfScopeAsync(admin, toSend, ct) is { } outOfScope) return outOfScope;

        var now = DateTimeOffset.UtcNow;
        var sent = 0;
        foreach (var entry in toSend.Where(e => e.ApprovedPayroll && !e.SentToPayroll))
        {
            entry.SentToPayroll = true;
            entry.SentByStaffId = admin.UserId;
            entry.SentByName = admin.DisplayName;
            entry.DateSentToPayroll = now;
            entries.Update(entry);
            await auditLog.LogAsync(admin, "TimesheetEntry.SentToPayroll", "TimesheetEntry", entry.Id,
                null, impersonatedUserId: null, ct);
            sent++;
        }

        await uow.SaveChangesAsync(ct);
        return new OkObjectResult(new { sent });
    }

    /// <summary>Admin: no restriction (null projectIds, HasNoAccess false). Non-admin: resolves the caller's own
    /// managed project(s); a PM managing nothing has HasNoAccess true so the caller can return its own
    /// appropriately-shaped empty 200 rather than a 403 - they're not forbidden, they just have nothing to see.</summary>
    private async Task<(IReadOnlyCollection<int>? ProjectIds, bool HasNoAccess)> ResolveScopeAsync(
        CurrentUserContext user, CancellationToken ct)
    {
        if (user.IsAdmin) return (null, false);

        var managed = await projects.GetManagedByUserAsync(user.UserId, ct);
        if (managed.Count == 0) return (null, true);
        return (managed.Select(p => p.Id).ToHashSet(), false);
    }

    /// <summary>For the bulk-mutate endpoints: an Admin caller may act on anything. A non-admin caller must have
    /// every loaded entry's project under their own management - reject the whole batch (400) rather than
    /// silently skipping the entries that don't belong to them, so there's no confusing partial-success result.</summary>
    private async Task<IActionResult?> CheckOutOfScopeAsync(CurrentUserContext user, IReadOnlyList<TimesheetEntry> loaded, CancellationToken ct)
    {
        if (user.IsAdmin) return null;

        var managed = await projects.GetManagedByUserAsync(user.UserId, ct);
        var managedProjectIds = managed.Select(p => p.Id).ToHashSet();
        if (loaded.Any(e => !managedProjectIds.Contains(e.ProjectId)))
        {
            return new BadRequestObjectResult(new { error = "One or more selected entries are outside the projects you manage." });
        }
        return null;
    }

    private static ApprovalEntryDto ToDto(Domain.Entities.TimesheetEntry e) => new(
        e.Id, e.UserId, e.User?.DisplayName ?? "", e.Date, e.Description,
        e.ProjectId, e.Project?.Name ?? "", e.ClientId, e.Client?.Name ?? "",
        e.WorkHours, e.OutOfHoursHours, e.ToPayroll,
        e.ApprovedPayroll, e.SentToPayroll, e.PostingBatch);
}
