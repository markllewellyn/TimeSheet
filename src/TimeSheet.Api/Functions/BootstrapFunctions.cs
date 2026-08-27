using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>
/// Solves the bootstrap problem: Admins invite Users, but on a brand-new database nobody is an Admin yet.
/// Both endpoints here are only reachable while the Users table is completely empty (see
/// CurrentUserMiddleware) - the moment either creates the first (Admin) User, both permanently 403 for
/// everyone else, including a second caller racing to also become "first" admin.
/// </summary>
public class BootstrapFunctions(IUserRepository users, IPasswordHasher passwordHasher, IUnitOfWork uow)
{
    /// <summary>Requires a valid Entra (or local) token - see BootstrapFunctions/CurrentUserMiddleware.</summary>
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
            PayrollNumber = GeneratePlaceholderPayrollNumber(),
            Role = Domain.UserRole.Admin,
            IsActive = true,
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        await users.AddAsync(admin, ct);
        await uow.SaveChangesAsync(ct);

        return new OkObjectResult(new { admin.Id, admin.DisplayName, admin.Email, Role = admin.Role.ToString() });
    }

    /// <summary>Genuinely anonymous - no token required at all, since this exists specifically for the case
    /// where Entra SSO isn't set up/working yet. Only reachable while the Users table is empty.</summary>
    [Function("Bootstrap_FirstLocalAdmin")]
    public async Task<IActionResult> FirstLocalAdmin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "bootstrap/first-local-admin")] HttpRequest req, CancellationToken ct)
    {
        if ((await users.GetAllAsync(includeInactive: true, ct)).Count > 0)
        {
            return new ObjectResult(new { error = "Bootstrap is only available before the first user has been created." })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
        }

        var body = await req.ReadFromJsonAsync<FirstLocalAdminRequest>(ct);
        if (body is null || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password) || string.IsNullOrWhiteSpace(body.DisplayName))
        {
            return new BadRequestObjectResult(new { error = "Email, password, and displayName are required." });
        }

        if (body.Password.Length < 8)
        {
            return new BadRequestObjectResult(new { error = "Password must be at least 8 characters." });
        }

        var admin = new User
        {
            Email = body.Email,
            DisplayName = body.DisplayName,
            PasswordHash = passwordHasher.Hash(body.Password),
            PayrollNumber = GeneratePlaceholderPayrollNumber(),
            Role = Domain.UserRole.Admin,
            IsActive = true,
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        await users.AddAsync(admin, ct);
        await uow.SaveChangesAsync(ct);

        return new OkObjectResult(new { admin.Id, admin.DisplayName, admin.Email, Role = admin.Role.ToString() });
    }

    // Staff.PayrollNumber is a required legacy column with no equivalent concept in the app's own flows -
    // generate a placeholder (varchar(10), so a 10-char hex slice of a GUID) rather than block on a real value
    // that would need to come from an actual payroll system integration this app doesn't have.
    private static string GeneratePlaceholderPayrollNumber() => Guid.NewGuid().ToString("N")[..10];
}

public record FirstLocalAdminRequest(string Email, string Password, string DisplayName);
