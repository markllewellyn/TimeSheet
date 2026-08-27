using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>The two-stage payroll workflow mirrored from the legacy Power App's Admin Overview screen: approve
/// raw timesheet entries (grouping them into a named Posting Batch), then separately mark the approved batch as
/// sent to payroll. Distinct from the budget-overrun Escalations queue - this approves the entry itself, not a
/// budget exception. Admin-only throughout.</summary>
public class TimesheetApprovalFunctions(
    ITimesheetEntryRepository entries,
    IUnitOfWork uow,
    ICurrentUserAccessor currentUser)
{
    [Function("Approvals_Pending")]
    public async Task<IActionResult> Pending(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "approvals/pending")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var search = req.Query["search"].ToString();
        var pending = await entries.GetPendingApprovalAsync(string.IsNullOrWhiteSpace(search) ? null : search, ct);
        var batches = await entries.GetDistinctPostingBatchesAsync(ct);

        return new OkObjectResult(new PendingApprovalsResponse(pending.Select(ToDto).ToList(), batches));
    }

    [Function("Approvals_Approve")]
    public async Task<IActionResult> Approve(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "approvals/approve")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

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

        var admin = currentUser.RequireUser();
        var now = DateTimeOffset.UtcNow;
        var toApprove = await entries.GetByIdsAsync(body.EntryIds, ct);

        var approved = 0;
        foreach (var entry in toApprove.Where(e => !e.ApprovedPayroll))
        {
            entry.ApprovedPayroll = true;
            entry.ApprovedByStaffId = admin.UserId;
            entry.ApprovedByName = admin.DisplayName;
            entry.DateApprovedPayroll = now;
            entry.PostingBatch = body.PostingBatch;
            entries.Update(entry);
            approved++;
        }

        await uow.SaveChangesAsync(ct);
        return new OkObjectResult(new { approved });
    }

    [Function("Approvals_ReadyForPayroll")]
    public async Task<IActionResult> ReadyForPayroll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "approvals/ready-for-payroll")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var ready = await entries.GetReadyForPayrollAsync(ct);
        return new OkObjectResult(ready.Select(ToDto));
    }

    [Function("Approvals_SendToPayroll")]
    public async Task<IActionResult> SendToPayroll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "approvals/send-to-payroll")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var body = await req.ReadFromJsonAsync<SendToPayrollRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        if (body.EntryIds is null or { Length: 0 })
        {
            return new BadRequestObjectResult(new { error = "At least one entry must be selected." });
        }

        var admin = currentUser.RequireUser();
        var now = DateTimeOffset.UtcNow;
        var toSend = await entries.GetByIdsAsync(body.EntryIds, ct);

        var sent = 0;
        foreach (var entry in toSend.Where(e => e.ApprovedPayroll && !e.SentToPayroll))
        {
            entry.SentToPayroll = true;
            entry.SentByStaffId = admin.UserId;
            entry.SentByName = admin.DisplayName;
            entry.DateSentToPayroll = now;
            entries.Update(entry);
            sent++;
        }

        await uow.SaveChangesAsync(ct);
        return new OkObjectResult(new { sent });
    }

    private static ApprovalEntryDto ToDto(Domain.Entities.TimesheetEntry e) => new(
        e.Id, e.UserId, e.User?.DisplayName ?? "", e.Date, e.Description,
        e.ProjectId, e.Project?.Name ?? "", e.ClientId, e.Client?.Name ?? "",
        e.WorkHours, e.OutOfHoursHours, e.ToPayroll,
        e.ApprovedPayroll, e.SentToPayroll, e.PostingBatch);
}
