using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Sign-in for local (username/password) accounts - a fallback path alongside Entra SSO. See
/// ILocalAuthService/CurrentUserMiddleware for how the issued token is later validated.</summary>
public class AuthFunctions(ILocalAuthService localAuth)
{
    [Function("Auth_LocalLogin")]
    public async Task<IActionResult> LocalLogin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/local-login")] HttpRequest req, CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<LocalLoginRequest>(ct);
        if (body is null || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
        {
            return new BadRequestObjectResult(new { error = "Email and password are required." });
        }

        var result = await localAuth.LoginAsync(body.Email, body.Password, ct);
        if (result is null)
        {
            return new UnauthorizedObjectResult(new { error = "Invalid email or password." });
        }

        return new OkObjectResult(new { token = result.Token, expiresAtUtc = result.ExpiresAtUtc });
    }
}

public record LocalLoginRequest(string Email, string Password);
