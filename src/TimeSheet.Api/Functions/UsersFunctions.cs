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

public class UsersFunctions(IUserRepository users, IUnitOfWork uow, ICurrentUserAccessor currentUser)
{
    [Function("Users_List")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var includeInactive = req.Query["includeInactive"] == "true";
        var result = await users.GetAllAsync(includeInactive, ct);
        return new OkObjectResult(result.Select(ToDto));
    }

    /// <summary>Admins pre-create User rows (invite-style); an authenticated Entra principal with no matching
    /// User row is rejected 403 "not provisioned" by CurrentUserMiddleware, never auto-provisioned.</summary>
    [Function("Users_Invite")]
    public async Task<IActionResult> Invite(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "users")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var body = await req.ReadFromJsonAsync<InviteUserRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        if (!Enum.TryParse<UserRole>(body.Role, out var role))
        {
            return new BadRequestObjectResult(new { error = "Invalid Role - must be 'Admin' or 'User'." });
        }

        if (await users.GetByEmailAsync(body.Email, ct) is not null)
        {
            return new ConflictObjectResult(new { error = $"A user with email '{body.Email}' already exists." });
        }

        var user = new User
        {
            EntraObjectId = body.EntraObjectId,
            Email = body.Email,
            DisplayName = body.DisplayName,
            Role = role,
            JobTitle = body.JobTitle,
            IsActive = true,
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        await users.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);

        return new CreatedResult($"/api/users/{user.Id}", ToDto(user));
    }

    [Function("Users_Update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "users/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var user = await users.GetByIdAsync(id, ct);
        if (user is null) return new NotFoundResult();

        var body = await req.ReadFromJsonAsync<UpdateUserRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        if (!Enum.TryParse<UserRole>(body.Role, out var role))
        {
            return new BadRequestObjectResult(new { error = "Invalid Role - must be 'Admin' or 'User'." });
        }

        user.DisplayName = body.DisplayName;
        user.Role = role;
        user.JobTitle = body.JobTitle;
        user.IsActive = body.IsActive;
        user.ModifiedUtc = DateTimeOffset.UtcNow;

        users.Update(user);
        await uow.SaveChangesAsync(ct);
        return new OkObjectResult(ToDto(user));
    }

    private static UserDto ToDto(User u) => new(u.Id, u.EntraObjectId, u.Email, u.DisplayName, u.Role.ToString(), u.JobTitle, u.IsActive);
}
