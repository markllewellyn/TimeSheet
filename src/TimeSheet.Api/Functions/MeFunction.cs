using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Called by the Angular app right after MSAL login to get the app's own User profile (Role, display
/// info) - this, not Entra ID token claims, is the client-side authorization source of truth.</summary>
public class MeFunction(ICurrentUserAccessor currentUser)
{
    [Function("Me")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")] HttpRequest req)
    {
        var user = currentUser.RequireUser();
        return new OkObjectResult(new MeDto(user.UserId, user.DisplayName, user.Email, user.Role.ToString()));
    }
}
