using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Staff-grouping Teams (e.g. "Delivery Pod A") - see Team.cs. Admin-only, mirrors RolesFunctions
/// exactly - a plain grouping with no billing significance, unlike Role.</summary>
public class TeamsFunctions(ITeamRepository teams, IUnitOfWork uow, ICurrentUserAccessor currentUser)
{
    [Function("Teams_List")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "teams")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var includeInactive = req.Query["includeInactive"] == "true";
        var result = await teams.GetAllAsync(includeInactive, ct);
        return new OkObjectResult(result.Select(ToDto));
    }

    [Function("Teams_Create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "teams")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var body = await req.ReadFromJsonAsync<CreateTeamRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        if (string.IsNullOrWhiteSpace(body.Name))
        {
            return new BadRequestObjectResult(new { error = "Name is required." });
        }

        if (await teams.GetByNameAsync(body.Name, ct) is not null)
        {
            return new ConflictObjectResult(new { error = $"A team named '{body.Name}' already exists." });
        }

        var team = new Team { Name = body.Name, IsActive = true, CreatedUtc = DateTimeOffset.UtcNow };
        await teams.AddAsync(team, ct);
        await uow.SaveChangesAsync(ct);

        return new CreatedResult($"/api/teams/{team.Id}", ToDto(team));
    }

    [Function("Teams_Update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "teams/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var team = await teams.GetByIdAsync(id, ct);
        if (team is null) return new NotFoundResult();

        var body = await req.ReadFromJsonAsync<UpdateTeamRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        if (string.IsNullOrWhiteSpace(body.Name))
        {
            return new BadRequestObjectResult(new { error = "Name is required." });
        }

        var existing = await teams.GetByNameAsync(body.Name, ct);
        if (existing is not null && existing.Id != id)
        {
            return new ConflictObjectResult(new { error = $"A team named '{body.Name}' already exists." });
        }

        team.Name = body.Name;
        team.IsActive = body.IsActive;
        teams.Update(team);
        await uow.SaveChangesAsync(ct);

        return new OkObjectResult(ToDto(team));
    }

    private static TeamDto ToDto(Team t) => new(t.Id, t.Name, t.IsActive);
}
