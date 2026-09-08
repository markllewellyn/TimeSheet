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

public class ProjectsFunctions(
    IProjectRepository projects,
    IClientRepository clients,
    IUserRepository users,
    IStaffProjectRepository assignments,
    IProjectEstimateService estimateService,
    IUnitOfWork uow,
    ICurrentUserAccessor currentUser)
{
    [Function("Projects_Estimate")]
    public async Task<IActionResult> Estimate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{id:int}/estimate")] HttpRequest req, int id, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(id, ct);
        if (project is null) return new NotFoundResult();
        if (currentUser.RequireAdminOrProjectManager(project) is { } forbidden) return forbidden;

        var estimate = await estimateService.EstimateAsync(id, ct);
        return new OkObjectResult(new ProjectEstimateDto(
            estimate.ProjectId, estimate.BudgetHours, estimate.EstimatedCost, estimate.EstimatedRevenue, estimate.EstimatedProfit,
            estimate.Lines.Select(l => new ProjectEstimateLineDto(
                l.UserId, l.UserName, l.RoleId, l.RoleName, l.AllocatedHours,
                l.HourlyCost, l.CustomerRate, l.EstimatedCost, l.EstimatedRevenue, l.EstimatedProfit, l.Warning)).ToList()));
    }

    /// <summary>Flat, all-clients list of every active project - used by the Staff screen's project
    /// filter/picker so it doesn't need to replicate a per-client fan-out client-side.</summary>
    [Function("Projects_ListAll")]
    public async Task<IActionResult> ListAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var result = await projects.GetAllActiveAsync(ct);
        var counts = await assignments.GetActiveAssignmentCountsAsync(result.Select(p => p.Id).ToList(), ct);
        return new OkObjectResult(result.Select(p => ToDto(p, p.Client?.Name ?? "", assignedStaffCount: counts.GetValueOrDefault(p.Id))));
    }

    [Function("Projects_ListByClient")]
    public async Task<IActionResult> ListByClient(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clients/{clientId:int}/projects")] HttpRequest req, int clientId, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var client = await clients.GetByIdAsync(clientId, ct);
        if (client is null) return new NotFoundResult();

        var includeInactive = req.Query["includeInactive"] == "true";
        var result = await projects.GetByClientIdAsync(clientId, includeInactive, ct);
        var counts = await assignments.GetActiveAssignmentCountsAsync(result.Select(p => p.Id).ToList(), ct);
        return new OkObjectResult(result.Select(p => ToDto(p, client.Name, assignedStaffCount: counts.GetValueOrDefault(p.Id))));
    }

    [Function("Projects_ListAssignedToMe")]
    public async Task<IActionResult> ListAssignedToMe(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/assigned-to-me")] HttpRequest req, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var targetUserId = user.UserId;

        // Admin-only escape hatch for impersonation ("log time on behalf of X") - lets the Add Entry page show
        // the target person's assigned projects rather than the admin's own.
        if (req.Query.TryGetValue("userId", out var userIdRaw) && int.TryParse(userIdRaw, out var requestedUserId) && requestedUserId != user.UserId)
        {
            if (currentUser.RequireAdmin() is { } forbidden) return forbidden;
            targetUserId = requestedUserId;
        }

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
        var result = await projects.GetAssignedToUserAsync(targetUserId, today, ct);
        return new OkObjectResult(result.Select(p => ToDto(p, p.Client?.Name ?? "")));
    }

    [Function("Projects_Get")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(id, ct);
        if (project is null) return new NotFoundResult();

        var user = currentUser.RequireUser();
        if (!user.IsAdmin)
        {
            var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
            var assigned = await projects.GetAssignedToUserAsync(user.UserId, today, ct);
            if (!assigned.Any(p => p.Id == id))
            {
                return new ObjectResult(new { error = "Not assigned to this project." }) { StatusCode = StatusCodes.Status403Forbidden };
            }
        }

        var client = await clients.GetByIdAsync(project.ClientId, ct);
        var activeAssignments = await assignments.GetByProjectIdAsync(project.Id, activeOnly: true, ct);
        return new OkObjectResult(ToDto(project, client?.Name ?? "", assignedStaffCount: activeAssignments.Count));
    }

    [Function("Projects_Create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var body = await req.ReadFromJsonAsync<CreateProjectRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        var client = await clients.GetByIdAsync(body.ClientId, ct);
        if (client is null) return new NotFoundObjectResult(new { error = "Client not found." });

        if (await projects.CodeExistsForClientAsync(body.ClientId, body.Code, null, ct))
        {
            return new ConflictObjectResult(new { error = $"Project code '{body.Code}' is already used for this client." });
        }

        if (!Enum.TryParse<PaymentModel>(body.PaymentModel, out var paymentModel))
        {
            return new BadRequestObjectResult(new { error = "Invalid PaymentModel." });
        }
        if (!Enum.TryParse<ProjectType>(body.ProjectType, out var projectType))
        {
            return new BadRequestObjectResult(new { error = "Invalid ProjectType." });
        }

        var manager = await ResolveProjectManagerAsync(body.ProjectManagerUserId, ct);
        if (manager.Error is { } managerError) return managerError;

        var project = new Project
        {
            ClientId = body.ClientId,
            Name = body.Name,
            Code = body.Code,
            Description = body.Description,
            PaymentModel = paymentModel,
            ProjectType = projectType,
            CanInvoice = body.CanInvoice,
            IsCostExempt = body.IsCostExempt,
            CurrencyOverride = body.CurrencyOverride,
            StartDate = body.StartDate,
            EndDate = body.EndDate,
            BudgetHours = body.BudgetHours,
            FixedFeeAmount = body.FixedFeeAmount,
            IsActive = true,
            ProjectManagerUserId = manager.User?.Id,
            CreatedUtc = DateTimeOffset.UtcNow,
            CreatedByUserId = currentUser.RequireUser().UserId,
        };
        await projects.AddAsync(project, ct);
        await uow.SaveChangesAsync(ct);
        return new CreatedResult($"/api/projects/{project.Id}", ToDto(project, client.Name, manager.User?.DisplayName));
    }

    [Function("Projects_Update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "projects/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var project = await projects.GetByIdAsync(id, ct);
        if (project is null) return new NotFoundResult();

        var body = await req.ReadFromJsonAsync<UpdateProjectRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        if (!Enum.TryParse<ProjectType>(body.ProjectType, out var projectType))
        {
            return new BadRequestObjectResult(new { error = "Invalid ProjectType." });
        }

        var manager = await ResolveProjectManagerAsync(body.ProjectManagerUserId, ct);
        if (manager.Error is { } managerError) return managerError;

        project.Name = body.Name;
        project.Description = body.Description;
        project.ProjectType = projectType;
        project.CanInvoice = body.CanInvoice;
        project.IsCostExempt = body.IsCostExempt;
        project.CurrencyOverride = body.CurrencyOverride;
        project.EndDate = body.EndDate;
        project.BudgetHours = body.BudgetHours;
        project.FixedFeeAmount = body.FixedFeeAmount;
        project.IsActive = body.IsActive;
        project.ProjectManagerUserId = manager.User?.Id;
        project.ModifiedUtc = DateTimeOffset.UtcNow;

        projects.Update(project);
        await uow.SaveChangesAsync(ct);

        var client = await clients.GetByIdAsync(project.ClientId, ct);
        var activeAssignments = await assignments.GetByProjectIdAsync(project.Id, activeOnly: true, ct);
        return new OkObjectResult(ToDto(project, client?.Name ?? "", manager.User?.DisplayName, activeAssignments.Count));
    }

    /// <summary>Null userId means "no manager" (valid). A non-null userId must resolve to an existing user, or
    /// this returns the 404 to surface back to the caller.</summary>
    private async Task<(User? User, IActionResult? Error)> ResolveProjectManagerAsync(int? userId, CancellationToken ct)
    {
        if (userId is null) return (null, null);
        var user = await users.GetByIdAsync(userId.Value, ct);
        if (user is null) return (null, new NotFoundObjectResult(new { error = "Project manager user not found." }));
        return (user, null);
    }

    private static ProjectDto ToDto(Project p, string clientName, string? projectManagerName = null, int assignedStaffCount = 0) => new(
        p.Id, p.ClientId, clientName, p.Name, p.Code, p.Description,
        p.PaymentModel.ToString(), p.ProjectType.ToString(), p.CanInvoice, p.IsCostExempt, p.CurrencyOverride, p.StartDate, p.EndDate,
        p.BudgetHours, p.FixedFeeAmount, p.IsActive,
        p.ProjectManagerUserId, projectManagerName ?? p.ProjectManager?.DisplayName, assignedStaffCount);
}
