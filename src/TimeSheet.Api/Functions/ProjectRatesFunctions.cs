using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Cost/billing rates reveal internal margin - Admin-only throughout.</summary>
public class ProjectRatesFunctions(
    IProjectRateRepository rates,
    IProjectRepository projects,
    IUserRepository users,
    IUnitOfWork uow,
    ICurrentUserAccessor currentUser)
{
    [Function("ProjectRates_History")]
    public async Task<IActionResult> History(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{projectId:int}/rates")] HttpRequest req, int projectId, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        int? userId = req.Query.TryGetValue("userId", out var v) && int.TryParse(v, out var parsed) ? parsed : null;
        var history = await rates.GetHistoryAsync(projectId, userId, ct);

        var dtos = new List<ProjectRateDto>();
        foreach (var r in history)
        {
            string? userName = null;
            if (r.UserId is not null)
            {
                var u = await users.GetByIdAsync(r.UserId.Value, ct);
                userName = u?.DisplayName;
            }
            dtos.Add(ToDto(r, userName));
        }
        return new OkObjectResult(dtos);
    }

    [Function("ProjectRates_Create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:int}/rates")] HttpRequest req, int projectId, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var project = await projects.GetByIdAsync(projectId, ct);
        if (project is null) return new NotFoundResult();

        var body = await req.ReadFromJsonAsync<UpsertProjectRateRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        if (project.PaymentModel == Domain.PaymentModel.TimeAndMaterials && body.BillingRatePerHour is null)
        {
            return new BadRequestObjectResult(new { error = "BillingRatePerHour is required for Time and Materials projects." });
        }

        if (await rates.HasOverlapAsync(projectId, body.UserId, body.EffectiveFrom, body.EffectiveTo, null, ct))
        {
            return new ConflictObjectResult(new { error = "This rate's effective date range overlaps an existing rate for the same project/user." });
        }

        var rate = new ProjectRate
        {
            ProjectId = projectId,
            UserId = body.UserId,
            BillingRatePerHour = body.BillingRatePerHour,
            CostRatePerHour = body.CostRatePerHour,
            EffectiveFrom = body.EffectiveFrom,
            EffectiveTo = body.EffectiveTo,
            CreatedUtc = DateTimeOffset.UtcNow,
            CreatedByUserId = currentUser.RequireUser().UserId,
        };
        await rates.AddAsync(rate, ct);
        await uow.SaveChangesAsync(ct);

        string? userName = body.UserId is null ? null : (await users.GetByIdAsync(body.UserId.Value, ct))?.DisplayName;
        return new CreatedResult($"/api/projects/{projectId}/rates", ToDto(rate, userName));
    }

    private static ProjectRateDto ToDto(ProjectRate r, string? userName) => new(
        r.Id, r.ProjectId, r.UserId, userName, r.BillingRatePerHour, r.CostRatePerHour, r.EffectiveFrom, r.EffectiveTo);
}
