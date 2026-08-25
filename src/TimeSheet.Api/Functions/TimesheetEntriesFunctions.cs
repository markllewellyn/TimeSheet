using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Timesheet entries are always scoped to the signed-in caller - a User (or Admin, for their own
/// personal timesheet) can only ever see/create/edit their own entries. Admin-wide visibility across all
/// users' timesheets is a separate concern (Reporting/Escalations), not this endpoint.</summary>
public class TimesheetEntriesFunctions(
    ITimesheetEntryRepository entries,
    IProjectAssignmentRepository assignments,
    IProjectRepository projects,
    IUnitOfWork uow,
    ICurrentUserAccessor currentUser)
{
    [Function("TimesheetEntries_List")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "timesheet-entries")] HttpRequest req, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var search = req.Query["search"].ToString();
        var from = req.Query.TryGetValue("from", out var f) && DateOnly.TryParse(f, out var fd) ? fd : (DateOnly?)null;
        var to = req.Query.TryGetValue("to", out var t) && DateOnly.TryParse(t, out var td) ? td : (DateOnly?)null;

        var result = await entries.GetForUserAsync(user.UserId, search, from, to, ct);
        var dtos = result.Select(ToDto).ToList();

        return new OkObjectResult(new
        {
            entries = dtos,
            summary = BuildSummary(result),
        });
    }

    [Function("TimesheetEntries_Get")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "timesheet-entries/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var entry = await entries.GetByIdAsync(id, ct);
        if (entry is null || entry.UserId != user.UserId) return new NotFoundResult();
        return new OkObjectResult(ToDto(entry));
    }

    [Function("TimesheetEntries_Create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "timesheet-entries")] HttpRequest req, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var body = await req.ReadFromJsonAsync<CreateTimesheetEntryRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        var validation = await ValidateAssignmentAsync(user.UserId, body.ProjectId, body.Date, ct);
        if (validation is not null) return validation;

        var entry = new TimesheetEntry
        {
            UserId = user.UserId,
            ProjectId = body.ProjectId,
            Date = body.Date,
            WorkHours = body.WorkHours,
            OutOfHoursHours = body.OutOfHoursHours,
            Description = body.Description,
            Status = TimesheetEntryStatus.Normal,
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        await entries.AddAsync(entry, ct);
        await uow.SaveChangesAsync(ct);

        var saved = await entries.GetByIdAsync(entry.Id, ct);
        return new CreatedResult($"/api/timesheet-entries/{entry.Id}", ToDto(saved!));
    }

    [Function("TimesheetEntries_Update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "timesheet-entries/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var entry = await entries.GetByIdAsync(id, ct);
        if (entry is null || entry.UserId != user.UserId) return new NotFoundResult();

        var body = await req.ReadFromJsonAsync<UpdateTimesheetEntryRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        var validation = await ValidateAssignmentAsync(user.UserId, entry.ProjectId, body.Date, ct);
        if (validation is not null) return validation;

        entry.Date = body.Date;
        entry.WorkHours = body.WorkHours;
        entry.OutOfHoursHours = body.OutOfHoursHours;
        entry.Description = body.Description;
        entry.ModifiedUtc = DateTimeOffset.UtcNow;

        entries.Update(entry);
        await uow.SaveChangesAsync(ct);
        return new OkObjectResult(ToDto(entry));
    }

    [Function("TimesheetEntries_Delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "timesheet-entries/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var entry = await entries.GetByIdAsync(id, ct);
        if (entry is null || entry.UserId != user.UserId) return new NotFoundResult();

        entries.Remove(entry);
        await uow.SaveChangesAsync(ct);
        return new NoContentResult();
    }

    /// <summary>Mirrors the legacy app's row "duplicate" icon - clones an entry onto a (usually different) date.</summary>
    [Function("TimesheetEntries_Duplicate")]
    public async Task<IActionResult> Duplicate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "timesheet-entries/{id:int}/duplicate")] HttpRequest req, int id, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var source = await entries.GetByIdAsync(id, ct);
        if (source is null || source.UserId != user.UserId) return new NotFoundResult();

        var body = await req.ReadFromJsonAsync<DuplicateTimesheetEntryRequest>(ct);
        var targetDate = body?.Date ?? source.Date;

        var validation = await ValidateAssignmentAsync(user.UserId, source.ProjectId, targetDate, ct);
        if (validation is not null) return validation;

        var copy = new TimesheetEntry
        {
            UserId = user.UserId,
            ProjectId = source.ProjectId,
            Date = targetDate,
            WorkHours = source.WorkHours,
            OutOfHoursHours = source.OutOfHoursHours,
            Description = source.Description,
            Status = TimesheetEntryStatus.Normal,
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        await entries.AddAsync(copy, ct);
        await uow.SaveChangesAsync(ct);

        var saved = await entries.GetByIdAsync(copy.Id, ct);
        return new CreatedResult($"/api/timesheet-entries/{copy.Id}", ToDto(saved!));
    }

    /// <summary>Enforces "a User can only log time against a Project they're assigned to" - date-aware, checked
    /// at the service layer since assignment validity depends on the assignment's active date range, not just
    /// row existence (see IProjectAssignmentRepository.IsUserAssignedAsync).</summary>
    private async Task<IActionResult?> ValidateAssignmentAsync(int userId, int projectId, DateOnly date, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(projectId, ct);
        if (project is null) return new NotFoundObjectResult(new { error = "Project not found." });

        if (!await assignments.IsUserAssignedAsync(userId, projectId, date, ct))
        {
            return new ObjectResult(new { error = "You are not assigned to this project for the given date." })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
        }
        return null;
    }

    private static TimesheetEntrySummaryDto BuildSummary(IReadOnlyList<TimesheetEntry> result)
    {
        var mostRecentDate = result.Count == 0 ? (DateOnly?)null : result.Max(e => e.Date);
        var mostRecentDayHours = mostRecentDate is null
            ? 0
            : result.Where(e => e.Date == mostRecentDate).Sum(e => e.WorkHours + e.OutOfHoursHours);

        var totalWork = result.Sum(e => e.WorkHours);
        var totalOutOfHours = result.Sum(e => e.OutOfHoursHours);
        return new TimesheetEntrySummaryDto(mostRecentDayHours, totalWork, totalOutOfHours, totalWork + totalOutOfHours);
    }

    private static TimesheetEntryDto ToDto(TimesheetEntry e) => new(
        e.Id, e.ProjectId, e.Project?.Name ?? "", e.Project?.ClientId ?? 0, e.Project?.Client?.Name ?? "",
        e.Date, e.WorkHours, e.OutOfHoursHours, e.Description, e.Status.ToString(),
        e.Attachments.Select(a => new AttachmentDto(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedAtUtc)).ToList());
}

public record DuplicateTimesheetEntryRequest(DateOnly? Date);
