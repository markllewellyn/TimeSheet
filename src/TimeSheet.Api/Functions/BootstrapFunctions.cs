using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;

namespace TimeSheet.Api.Functions;

/// <summary>
/// Solves the bootstrap problem: Admins invite Users, but on a brand-new database nobody is an Admin yet.
/// CurrentUserMiddleware lets an authenticated-but-unprovisioned caller reach this one route ONLY while the
/// Users table is completely empty - the moment this creates the first (Admin) User, the table is no longer
/// empty and this endpoint permanently 403s for everyone else, including a second unprovisioned caller racing
/// to also become "first" admin.
/// </summary>
public class BootstrapFunctions(IUserRepository users, IUnitOfWork uow)
{
    [Function("Bootstrap_FirstAdmin")]
    public async Task<IActionResult> FirstAdmin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "bootstrap/first-admin")] HttpRequest req, CancellationToken ct)
    {
        if ((await users.GetAllAsync(includeInactive: true, ct)).Count > 0)
        {
            return new ObjectResult(new { error = "Bootstrap is only available before the first user has been created." })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
        }

        var principal = req.HttpContext.User;
        var oid = principal.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("oid");
        var email = principal.FindFirstValue(ClaimTypes.Upn)
            ?? principal.FindFirstValue("preferred_username")
            ?? principal.FindFirstValue(ClaimTypes.Email)
            ?? "";
        var displayName = principal.FindFirstValue(ClaimTypes.Name) ?? principal.Identity?.Name ?? email;

        if (string.IsNullOrEmpty(oid))
        {
            return new UnauthorizedResult();
        }

        var admin = new User
        {
            EntraObjectId = oid,
            Email = email,
            DisplayName = displayName,
            Role = Domain.UserRole.Admin,
            IsActive = true,
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        await users.AddAsync(admin, ct);
        await uow.SaveChangesAsync(ct);

        return new OkObjectResult(new { admin.Id, admin.DisplayName, admin.Email, Role = admin.Role.ToString() });
    }
}
