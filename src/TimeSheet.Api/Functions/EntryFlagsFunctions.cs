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
/// and counts normally throughout. System-raised (budget/allocation exceeded) or manually raised by an
/// admin/PM. An admin sees/acts on every flag; a PM is scoped to flags raised against their own project(s) -
/// see AuthorizationExtensions.RequireAdminOrProjectManager.</summary>
public class EntryFlagsFunctions(
    IEntryFlagRepository flags,
    ITimesheetEntryRepository entries,
    IProjectRepository projects,
    IEntryFlagService entryFlagService,
    IAuditLogService auditLog,
    IUnitOfWork uow,
    ICurrentUserAccessor currentUser)
{
    [Function("EntryFlags_Open")]
    public async Task<IActionResult> Open(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "entry-flags/open")] HttpRequest req, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var open = await flags.GetOpenAsync(ct);
        if (!user.IsAdmin)
        {
            open = open.Where(f => f.Project?.ProjectManagerUserId == user.UserId).ToList();
        }
        return new OkObjectResult(open.Select(ToDto));
    }

    /// <summary>Powers the "raise a flag" picker - an admin/PM search for an entry to flag by staff/client/
    /// project name or by its own numeric Entry Id, so they don't need to already know the id ahead of time (an
    /// id search still finds it exactly even so). Same PM-scoping precedent as
    /// TimesheetApprovalFunctions.ResolveScopeAsync: Admin sees everything, a PM is restricted to their own
    /// managed project(s), and a PM managing nothing simply gets an empty result rather than a 403.</summary>
    [Function("EntryFlags_SearchEntries")]
    public async Task<IActionResult> SearchEntries(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "entry-flags/search-entries")] HttpRequest req, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var search = req.Query["search"].ToString().Trim();
        // A numeric search is an exact Entry Id lookup, so even a single digit is a meaningful query; a
        // text search needs at least 2 characters to avoid an overly broad staff/client/project match.
        var isTooShort = search.Length < (int.TryParse(search, out _) ? 1 : 2);
        if (string.IsNullOrWhiteSpace(search) || isTooShort)
        {
            return new OkObjectResult(Array.Empty<EntryFlagSearchResultDto>());
        }

        IReadOnlyCollection<int>? projectIds = null;
        if (!user.IsAdmin)
        {
            var managed = await projects.GetManagedByUserAsync(user.UserId, ct);
            if (managed.Count == 0) return new OkObjectResult(Array.Empty<EntryFlagSearchResultDto>());
            projectIds = managed.Select(p => p.Id).ToHashSet();
        }

        var results = await entries.SearchForFlaggingAsync(search, projectIds, take: 25, ct);
        return new OkObjectResult(results.Select(e => new EntryFlagSearchResultDto(
            e.Id, e.UserId, e.User?.DisplayName ?? "", e.Date, e.Description,
            e.Project?.Name ?? "", e.Client?.Name ?? "", e.WorkHours, e.OutOfHoursHours)));
    }

    [Function("EntryFlags_RaiseManual")]
    public async Task<IActionResult> RaiseManual(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "entry-flags/raise")] HttpRequest req, CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<RaiseEntryFlagRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        var entry = await entries.GetByIdAsync(body.TimesheetEntryId, ct);
        if (entry is null) return new NotFoundObjectResult(new { error = "Timesheet entry not found." });
        if (currentUser.RequireAdminOrProjectManager(entry.Project!) is { } forbidden) return forbidden;

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
        var existing = await flags.GetByIdAsync(id, ct);
        if (existing is null) return new NotFoundResult();
        if (currentUser.RequireAdminOrProjectManager(existing.Project!) is { } forbidden) return forbidden;

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
        var existing = await flags.GetByIdAsync(id, ct);
        if (existing is null) return new NotFoundResult();
        if (currentUser.RequireAdminOrProjectManager(existing.Project!) is { } forbidden) return forbidden;

        await entryFlagService.NotifyStaffAsync(id, ct);
        return new OkResult();
    }

    private static EntryFlagDto ToDto(EntryFlag f) => new(
        f.Id, f.TimesheetEntryId, f.ProjectId, f.Project?.Name, f.Project?.Client?.Name,
        f.Reason.ToString(), f.BudgetLimitAtTimeOfEntry, f.CumulativeValueAtTimeOfEntry,
        f.RaisedByUserId, f.RaisedNotes, f.RaisedAtUtc,
        f.IsCleared, f.ClearedByUserId, f.ClearedAtUtc, f.ClearedNotes);
}
