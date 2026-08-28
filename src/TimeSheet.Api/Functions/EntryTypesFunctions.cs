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

/// <summary>Admin-only, scoped to one project at a time - see EntryType.cs. Deactivated (PUT), never deleted,
/// since a historical TimesheetEntry may reference a row.</summary>
public class EntryTypesFunctions(
    IEntryTypeRepository entryTypes,
    IProjectRepository projects,
    IUnitOfWork uow,
    ICurrentUserAccessor currentUser)
{
    [Function("EntryTypes_ListByProject")]
    public async Task<IActionResult> ListByProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{projectId:int}/entry-types")] HttpRequest req, int projectId, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var project = await projects.GetByIdAsync(projectId, ct);
        if (project is null) return new NotFoundResult();

        var includeInactive = req.Query["includeInactive"] == "true";
        var result = await entryTypes.GetByProjectIdAsync(projectId, includeInactive, ct);
        return new OkObjectResult(result.Select(ToDto));
    }

    [Function("EntryTypes_Create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:int}/entry-types")] HttpRequest req, int projectId, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var project = await projects.GetByIdAsync(projectId, ct);
        if (project is null) return new NotFoundResult();

        var body = await req.ReadFromJsonAsync<CreateEntryTypeRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        if (string.IsNullOrWhiteSpace(body.Name))
        {
            return new BadRequestObjectResult(new { error = "Name is required." });
        }
        if (body.IsContractType && project.ProjectType != ProjectType.Contract)
        {
            return new BadRequestObjectResult(new { error = "Only a Contract-type project can have a Contract entry type." });
        }

        var existing = await entryTypes.GetByProjectIdAsync(projectId, includeInactive: true, ct);
        if (existing.Any(t => string.Equals(t.Name, body.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return new ConflictObjectResult(new { error = $"This project already has an entry type named '{body.Name}'." });
        }

        var entryType = new EntryType
        {
            ProjectId = projectId,
            Name = body.Name,
            IsContractType = body.IsContractType,
            IsActive = true,
            CreatedUtc = DateTimeOffset.UtcNow,
            CreatedByUserId = currentUser.RequireUser().UserId,
        };
        await entryTypes.AddAsync(entryType, ct);
        await uow.SaveChangesAsync(ct);
        return new CreatedResult($"/api/entry-types/{entryType.Id}", ToDto(entryType));
    }

    [Function("EntryTypes_Update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "entry-types/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var entryType = await entryTypes.GetByIdAsync(id, ct);
        if (entryType is null) return new NotFoundResult();

        var body = await req.ReadFromJsonAsync<UpdateEntryTypeRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        if (string.IsNullOrWhiteSpace(body.Name))
        {
            return new BadRequestObjectResult(new { error = "Name is required." });
        }

        var existing = await entryTypes.GetByProjectIdAsync(entryType.ProjectId, includeInactive: true, ct);
        if (existing.Any(t => t.Id != id && string.Equals(t.Name, body.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return new ConflictObjectResult(new { error = $"This project already has an entry type named '{body.Name}'." });
        }

        entryType.Name = body.Name;
        entryType.IsActive = body.IsActive;
        entryTypes.Update(entryType);
        await uow.SaveChangesAsync(ct);
        return new OkObjectResult(ToDto(entryType));
    }

    private static EntryTypeDto ToDto(EntryType t) => new(t.Id, t.ProjectId, t.Name, t.IsContractType, t.IsActive);
}
