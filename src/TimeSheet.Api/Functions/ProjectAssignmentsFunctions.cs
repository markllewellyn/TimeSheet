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

public class ProjectAssignmentsFunctions(
    IStaffProjectRepository assignments,
    IProjectRepository projects,
    IUserRepository users,
    IRateResolver rateResolver,
    IUserWorkloadService workloadService,
    IUnitOfWork uow,
    ICurrentUserAccessor currentUser)
{
    [Function("ProjectAssignments_ListByProject")]
    public async Task<IActionResult> ListByProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{projectId:int}/assignments")] HttpRequest req, int projectId, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var activeOnly = req.Query["activeOnly"] != "false";
        var result = await assignments.GetByProjectIdAsync(projectId, activeOnly, ct);
        return new OkObjectResult(result.Select(ToDto));
    }

    /// <summary>A staff member's assignments across ALL their projects, with a resolved-rate preview per row -
    /// FDD's Staff screen ("the rate that will be applied on each project they are assigned to and whether
    /// that rate comes from their role or from an override"). Contrast with ListByProject above, which is
    /// scoped to one project's assignees.</summary>
    [Function("ProjectAssignments_ListByUser")]
    public async Task<IActionResult> ListByUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users/{userId:int}/assignments")] HttpRequest req, int userId, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
        var result = await assignments.GetByUserIdAsync(userId, activeOnly: false, ct);

        var dtos = new List<StaffAssignmentDto>();
        foreach (var a in result)
        {
            try
            {
                var resolution = await rateResolver.ResolveAsync(userId, a.Project!.ClientId, a.ProjectId, today, ct);
                dtos.Add(ToStaffAssignmentDto(a, resolution.CustomerRate, ToRateSourceLabel(resolution.Tier), null));
            }
            catch (RateNotConfiguredException ex)
            {
                dtos.Add(ToStaffAssignmentDto(a, null, null, ex.Message));
            }
        }
        return new OkObjectResult(dtos);
    }

    [Function("ProjectAssignments_Create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "assignments")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var body = await req.ReadFromJsonAsync<CreateProjectAssignmentRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        var project = await projects.GetByIdAsync(body.ProjectId, ct);
        if (project is null) return new NotFoundObjectResult(new { error = "Project not found." });
        var user = await users.GetByIdAsync(body.UserId, ct);
        if (user is null) return new NotFoundObjectResult(new { error = "User not found." });

        // Assignment eligibility requires that IRateResolver can produce SOME rate for this person on this
        // project (a role-tier default counts) - not that a specific person-level override already exists.
        try
        {
            await rateResolver.ResolveAsync(body.UserId, project.ClientId, body.ProjectId, DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date), ct);
        }
        catch (RateNotConfiguredException ex)
        {
            return new ObjectResult(new { error = $"{user.DisplayName} has no billable rate configured yet - {ex.Message}" })
            {
                StatusCode = StatusCodes.Status409Conflict,
            };
        }

        // Block a date-range overlap against this (staff,project) pair's ACTIVE assignment only - an
        // ended assignment is history and imposes no restriction, however its old dates compare. New
        // assignments are always open-ended at creation (no EndDate on CreateProjectAssignmentRequest), so
        // pass endDate: null. The filtered unique index (see StaffProjectConfiguration) is defense-in-depth
        // for "at most one active", but can't return a clean error message, so it's enforced here first.
        if (await assignments.IsOverlappingAsync(body.UserId, body.ProjectId, body.StartDate, endDate: null, ct))
        {
            return new ConflictObjectResult(new { error = "This user already has an active assignment to this project that overlaps with the selected start date." });
        }

        var assignment = new StaffProject
        {
            StaffId = body.UserId,
            ProjectId = body.ProjectId,
            IsActive = true,
            StartDate = body.StartDate,
            AllocatedHoursPerWeek = body.AllocatedHoursPerWeek,
            Notes = body.Notes,
            CreatedUtc = DateTimeOffset.UtcNow,
            CreatedByUserId = currentUser.RequireUser().UserId,
        };
        await assignments.AddAsync(assignment, ct);
        await uow.SaveChangesAsync(ct);

        return new CreatedResult($"/api/assignments/{assignment.Id}", ToDtoWith(assignment, project, user));
    }

    [Function("ProjectAssignments_Update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "assignments/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var assignment = await assignments.GetByIdAsync(id, ct);
        if (assignment is null) return new NotFoundResult();

        var body = await req.ReadFromJsonAsync<UpdateProjectAssignmentRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        // The legacy [StaffProjects] table only has an Active bit - the old Active/Paused/Ended distinction
        // collapses to it (anything other than "Active" is treated as inactive).
        assignment.IsActive = body.Status == "Active";
        assignment.EndDate = body.EndDate;
        assignment.AllocatedHoursPerWeek = body.AllocatedHoursPerWeek;
        assignment.Notes = body.Notes;

        assignments.Update(assignment);
        await uow.SaveChangesAsync(ct);
        return new OkObjectResult(ToDto(assignment));
    }

    /// <summary>Sums AllocatedHoursPerWeek across the caller's active assignments overlapping the given week -
    /// deliberately explicit, not derived from remaining budget / remaining weeks. See IUserWorkloadService.</summary>
    [Function("Me_EstimatedHoursThisWeek")]
    public async Task<IActionResult> EstimatedHoursThisWeek(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me/estimated-hours-this-week")] HttpRequest req, CancellationToken ct)
    {
        var user = currentUser.RequireUser();

        var weekStart = req.Query.TryGetValue("weekStart", out var v) && DateOnly.TryParse(v, out var parsed)
            ? parsed
            : StartOfIsoWeek(DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date));

        var workload = await workloadService.GetEstimatedHoursThisWeekAsync(user.UserId, weekStart, ct);

        return new OkObjectResult(new EstimatedWeeklyWorkloadDto(
            weekStart,
            workload.EstimatedHoursThisWeek,
            workload.ByProject.Select(p => new ProjectAllocationLineDto(p.ProjectId, p.ProjectName, p.ClientName, p.AllocatedHoursPerWeek)).ToList(),
            workload.AssignmentsMissingAllocation));
    }

    private static DateOnly StartOfIsoWeek(DateOnly date)
    {
        var diff = (7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return date.AddDays(-diff);
    }

    private static ProjectAssignmentDto ToDto(StaffProject a) => new(
        a.Id, a.ProjectId, a.Project?.Name ?? "", a.StaffId, a.Staff?.DisplayName ?? "",
        a.IsActive ? "Active" : "Ended", a.StartDate, a.EndDate, a.AllocatedHoursPerWeek, a.Notes);

    private static StaffAssignmentDto ToStaffAssignmentDto(StaffProject a, decimal? customerRate, string? rateSource, string? rateWarning) => new(
        a.Id, a.ProjectId, a.Project?.Name ?? "", a.Project?.Client?.Name ?? "",
        a.IsActive ? "Active" : "Ended", a.StartDate, a.EndDate, a.AllocatedHoursPerWeek, a.Notes,
        customerRate, rateSource, rateWarning);

    /// <summary>Presentation-only labels for RateCardTier - "whether that rate comes from their role or from
    /// an override" (FDD). Kept here rather than on the enum itself so the domain layer stays free of display
    /// concerns.</summary>
    private static string ToRateSourceLabel(RateCardTier tier) => tier switch
    {
        RateCardTier.PersonProject => "Person override (this project)",
        RateCardTier.PersonClient => "Person override (this client)",
        RateCardTier.RoleProject => "Role override (this project)",
        RateCardTier.RoleClient => "Role override (this client)",
        RateCardTier.RoleDefault => "Role default",
        _ => tier.ToString(),
    };

    private static ProjectAssignmentDto ToDtoWith(StaffProject a, Project project, User user) => new(
        a.Id, a.ProjectId, project.Name, user.Id, user.DisplayName,
        a.IsActive ? "Active" : "Ended", a.StartDate, a.EndDate, a.AllocatedHoursPerWeek, a.Notes);
}
