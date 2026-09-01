using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Called by the Angular app right after MSAL login to get the app's own User profile (Role, display
/// info) - this, not Entra ID token claims, is the client-side authorization source of truth. IsProjectManager
/// tells the frontend whether to show PM-only surfaces (My Invoices, Approvals) even for a non-admin.</summary>
public class MeFunction(ICurrentUserAccessor currentUser, IProjectRepository projects)
{
    [Function("Me")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")] HttpRequest req, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var managed = await projects.GetManagedByUserAsync(user.UserId, ct);
        return new OkObjectResult(new MeDto(user.UserId, user.DisplayName, user.Email, user.Role.ToString(), managed.Count > 0));
    }
}
