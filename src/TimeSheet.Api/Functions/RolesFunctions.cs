using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Job-function roles (e.g. Developer, Consultant) that RateCards attach to - see Role.cs. Admin-only.</summary>
public class RolesFunctions(IRoleRepository roles, IUnitOfWork uow, ICurrentUserAccessor currentUser)
{
    [Function("Roles_List")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "roles")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var includeInactive = req.Query["includeInactive"] == "true";
        var result = await roles.GetAllAsync(includeInactive, ct);
        return new OkObjectResult(result.Select(ToDto));
    }

    [Function("Roles_Create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "roles")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var body = await req.ReadFromJsonAsync<CreateRoleRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        if (string.IsNullOrWhiteSpace(body.Name))
        {
            return new BadRequestObjectResult(new { error = "Name is required." });
        }

        if (await roles.GetByNameAsync(body.Name, ct) is not null)
        {
            return new ConflictObjectResult(new { error = $"A role named '{body.Name}' already exists." });
        }

        var role = new Role { Name = body.Name, IsActive = true, CreatedUtc = DateTimeOffset.UtcNow };
        await roles.AddAsync(role, ct);
        await uow.SaveChangesAsync(ct);

        return new CreatedResult($"/api/roles/{role.Id}", ToDto(role));
    }

    [Function("Roles_Update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "roles/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var role = await roles.GetByIdAsync(id, ct);
        if (role is null) return new NotFoundResult();

        var body = await req.ReadFromJsonAsync<UpdateRoleRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        if (string.IsNullOrWhiteSpace(body.Name))
        {
            return new BadRequestObjectResult(new { error = "Name is required." });
        }

        var existing = await roles.GetByNameAsync(body.Name, ct);
        if (existing is not null && existing.Id != id)
        {
            return new ConflictObjectResult(new { error = $"A role named '{body.Name}' already exists." });
        }

        role.Name = body.Name;
        role.IsActive = body.IsActive;
        roles.Update(role);
        await uow.SaveChangesAsync(ct);

        return new OkObjectResult(ToDto(role));
    }

    private static RoleDto ToDto(Role r) => new(r.Id, r.Name, r.IsActive);
}
