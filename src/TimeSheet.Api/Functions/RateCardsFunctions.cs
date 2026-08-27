using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Effective-dated, role-or-person-scoped billing rates - see RateCard.cs. Admin-only, since these
/// reveal internal margin/pricing. A "change" is always a new dated row - there is no update endpoint.</summary>
public class RateCardsFunctions(
    IRateCardRepository rateCards,
    IRoleRepository roles,
    IUserRepository users,
    IClientRepository clients,
    IProjectRepository projects,
    IUnitOfWork uow,
    ICurrentUserAccessor currentUser)
{
    [Function("RateCards_List")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rate-cards")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        int? staffId = req.Query.TryGetValue("staffId", out var s) && int.TryParse(s, out var sv) ? sv : null;
        int? roleId = req.Query.TryGetValue("roleId", out var r) && int.TryParse(r, out var rv) ? rv : null;
        int? clientId = req.Query.TryGetValue("clientId", out var c) && int.TryParse(c, out var cv) ? cv : null;
        int? projectId = req.Query.TryGetValue("projectId", out var p) && int.TryParse(p, out var pv) ? pv : null;

        var result = await rateCards.ListAsync(staffId, roleId, clientId, projectId, ct);
        return new OkObjectResult(result.Select(ToDto));
    }

    [Function("RateCards_Create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "rate-cards")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var body = await req.ReadFromJsonAsync<CreateRateCardRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        // Enforces RateCard's scope shape (see RateCard.cs) - the DB check constraints are defense-in-depth,
        // this is what returns a clean 400 instead of a raw DB error.
        if ((body.RoleId is null) == (body.StaffId is null))
        {
            return new BadRequestObjectResult(new { error = "Exactly one of RoleId or StaffId must be set." });
        }
        if (body.ClientId is not null && body.ProjectId is not null)
        {
            return new BadRequestObjectResult(new { error = "ClientId and ProjectId cannot both be set." });
        }
        if (body.StaffId is not null && body.ClientId is null && body.ProjectId is null)
        {
            return new BadRequestObjectResult(new { error = "A person-scoped rate must specify a Client or a Project." });
        }
        if (body.Rate <= 0)
        {
            return new BadRequestObjectResult(new { error = "Rate must be a positive number." });
        }

        Role? role = null;
        User? staff = null;
        if (body.RoleId is { } roleId)
        {
            role = await roles.GetByIdAsync(roleId, ct);
            if (role is null) return new NotFoundObjectResult(new { error = "Role not found." });
        }
        if (body.StaffId is { } staffId)
        {
            staff = await users.GetByIdAsync(staffId, ct);
            if (staff is null) return new NotFoundObjectResult(new { error = "Staff member not found." });
        }
        Client? client = null;
        if (body.ClientId is { } clientId)
        {
            client = await clients.GetByIdAsync(clientId, ct);
            if (client is null) return new NotFoundObjectResult(new { error = "Client not found." });
        }
        Project? project = null;
        if (body.ProjectId is { } projectId)
        {
            project = await projects.GetByIdAsync(projectId, ct);
            if (project is null) return new NotFoundObjectResult(new { error = "Project not found." });
        }

        var existing = await rateCards.FindAsync(body.RoleId, body.StaffId, body.ClientId, body.ProjectId, body.EffectiveFrom, ct);
        if (existing is not null && existing.EffectiveFrom == body.EffectiveFrom)
        {
            return new ConflictObjectResult(new { error = $"A rate for this exact scope is already effective from {body.EffectiveFrom:yyyy-MM-dd}." });
        }

        var rateCard = new RateCard
        {
            RoleId = body.RoleId,
            StaffId = body.StaffId,
            ClientId = body.ClientId,
            ProjectId = body.ProjectId,
            Rate = body.Rate,
            EffectiveFrom = body.EffectiveFrom,
            CreatedUtc = DateTimeOffset.UtcNow,
            CreatedByUserId = currentUser.RequireUser().UserId,
        };
        await rateCards.AddAsync(rateCard, ct);
        await uow.SaveChangesAsync(ct);

        return new CreatedResult($"/api/rate-cards/{rateCard.Id}", ToDtoWith(rateCard, role, staff, client, project));
    }

    private static RateCardDto ToDto(RateCard rc) => new(
        rc.Id, rc.RoleId, rc.Role?.Name, rc.StaffId, rc.Staff?.DisplayName,
        rc.ClientId, rc.Client?.Name, rc.ProjectId, rc.Project?.Name,
        rc.Rate, rc.EffectiveFrom, rc.CreatedUtc);

    private static RateCardDto ToDtoWith(RateCard rc, Role? role, User? staff, Client? client, Project? project) => new(
        rc.Id, rc.RoleId, role?.Name, rc.StaffId, staff?.DisplayName,
        rc.ClientId, client?.Name, rc.ProjectId, project?.Name,
        rc.Rate, rc.EffectiveFrom, rc.CreatedUtc);
}
