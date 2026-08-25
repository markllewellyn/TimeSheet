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
    IProjectAssignmentRepository assignments,
    IProjectRepository projects,
    IUserRepository users,
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

        // At most one Active/Paused assignment per (project,user) at a time - filtered unique index is
        // defense-in-depth; check here first so we can return a clean 409 instead of a raw DB constraint error.
        var existing = await assignments.GetByUserIdAsync(body.UserId, activeOnly: false, ct);
        if (existing.Any(a => a.ProjectId == body.ProjectId && a.Status != AssignmentStatus.Ended))
        {
            return new ConflictObjectResult(new { error = "This user already has an active or paused assignment to this project." });
        }

        var assignment = new ProjectAssignment
        {
            ProjectId = body.ProjectId,
            UserId = body.UserId,
            Status = AssignmentStatus.Active,
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

        if (!Enum.TryParse<AssignmentStatus>(body.Status, out var status))
        {
            return new BadRequestObjectResult(new { error = "Invalid Status." });
        }

        assignment.Status = status;
        assignment.EndDate = body.EndDate;
        assignment.AllocatedHoursPerWeek = body.AllocatedHoursPerWeek;
        assignment.Notes = body.Notes;

        assignments.Update(assignment);
        await uow.SaveChangesAsync(ct);
        return new OkObjectResult(ToDto(assignment));
    }

    /// <summary>Sums AllocatedHoursPerWeek across the caller's Active assignments overlapping the given week -
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

    private static ProjectAssignmentDto ToDto(ProjectAssignment a) => new(
        a.Id, a.ProjectId, a.Project?.Name ?? "", a.UserId, a.User?.DisplayName ?? "",
        a.Status.ToString(), a.StartDate, a.EndDate, a.AllocatedHoursPerWeek, a.Notes);

    private static ProjectAssignmentDto ToDtoWith(ProjectAssignment a, Project project, User user) => new(
        a.Id, a.ProjectId, project.Name, a.UserId, user.DisplayName,
        a.Status.ToString(), a.StartDate, a.EndDate, a.AllocatedHoursPerWeek, a.Notes);
}
