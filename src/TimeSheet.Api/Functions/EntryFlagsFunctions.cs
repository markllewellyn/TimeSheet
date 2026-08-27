using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>A flag is a prompt to query an entry, never a gate (FDD) - the entry it's raised against is saved
/// and counts normally throughout. System-raised (budget/allocation exceeded) or manually raised by an admin/PM
/// (PM-level access deferred until ProjectManager exists). Admin-only throughout for now.</summary>
public class EntryFlagsFunctions(
    IEntryFlagRepository flags,
    IEntryFlagService entryFlagService,
    IAuditLogService auditLog,
    IUnitOfWork uow,
    ICurrentUserAccessor currentUser)
{
    [Function("EntryFlags_Open")]
    public async Task<IActionResult> Open(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "entry-flags/open")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var open = await flags.GetOpenAsync(ct);
        return new OkObjectResult(open.Select(ToDto));
    }

    [Function("EntryFlags_RaiseManual")]
    public async Task<IActionResult> RaiseManual(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "entry-flags/raise")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var body = await req.ReadFromJsonAsync<RaiseEntryFlagRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        var admin = currentUser.RequireUser();
        var flag = await entryFlagService.RaiseManualAsync(body.TimesheetEntryId, admin.UserId, body.Notes, ct);
        // RaiseManualAsync already saved the flag itself; the audit row is a separate save (see Create's
        // matching comment in TimesheetEntriesFunctions).
        await auditLog.LogAsync(admin, "EntryFlag.Raised", "TimesheetEntry", body.TimesheetEntryId, body.Notes, impersonatedUserId: null, ct);
        await uow.SaveChangesAsync(ct);
        return new OkObjectResult(ToDto(flag));
    }

    [Function("EntryFlags_Clear")]
    public async Task<IActionResult> Clear(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "entry-flags/{id:int}/clear")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var body = await req.ReadFromJsonAsync<ClearEntryFlagRequest>(ct);
        var admin = currentUser.RequireUser();
        var flag = await entryFlagService.ClearAsync(id, admin.UserId, body?.Notes, ct);
        await auditLog.LogAsync(admin, "EntryFlag.Cleared", "TimesheetEntry", flag.TimesheetEntryId, body?.Notes, impersonatedUserId: null, ct);
        await uow.SaveChangesAsync(ct);
        return new OkObjectResult(ToDto(flag));
    }

    [Function("EntryFlags_NotifyStaff")]
    public async Task<IActionResult> NotifyStaff(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "entry-flags/{id:int}/notify-staff")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        await entryFlagService.NotifyStaffAsync(id, ct);
        return new OkResult();
    }

    private static EntryFlagDto ToDto(EntryFlag f) => new(
        f.Id, f.TimesheetEntryId, f.ProjectId, f.Project?.Name, f.Project?.Client?.Name,
        f.Reason.ToString(), f.BudgetLimitAtTimeOfEntry, f.CumulativeValueAtTimeOfEntry,
        f.RaisedByUserId, f.RaisedNotes, f.RaisedAtUtc,
        f.IsCleared, f.ClearedByUserId, f.ClearedAtUtc, f.ClearedNotes);
}
