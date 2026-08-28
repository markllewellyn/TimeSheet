using Microsoft.Graph.Models;
using TimeSheet.Domain.Services;
using TimeSheet.Infrastructure.Services;

namespace TimeSheet.Infrastructure.ExternalServices;

/// <summary>
/// Uses the Graph-admin app registration's User-PasswordProfile.ReadWrite.All application permission (prefer
/// this narrower permission over the broad User.ReadWrite.All - see the plan's Authentication section). This
/// permission is highly privileged (tenant-wide password write); its secret lives Api-side only and every
/// reset should be audit-logged by the caller (the Admin endpoint logs who reset whom).
/// </summary>
public class GraphAdminUserService(GraphClientFactory graphClientFactory) : IAdminUserService
{
    public async Task<string> ForcePasswordResetAsync(string entraObjectId, CancellationToken ct)
    {
        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        var client = graphClientFactory.CreateClient();

        // Both password and forceChangePasswordNextSignIn must be sent together - setting the flag alone
        // without a new password does not reliably take effect via Graph.
        await client.Users[entraObjectId].PatchAsync(new User
        {
            PasswordProfile = new PasswordProfile
            {
                ForceChangePasswordNextSignIn = true,
                Password = temporaryPassword,
            },
        }, cancellationToken: ct);

        return temporaryPassword;
    }

    /// <summary>Uses Graph's $search (not $filter) across displayName/mail, so a partial/misspelled query
    /// still surfaces reasonable matches - requires the ConsistencyLevel: eventual header, per Graph's own
    /// requirement for advanced query capabilities like $search. Requires the User.Read.All application
    /// permission (admin consent) on the same Graph-admin app registration - narrower than Directory.Read.All,
    /// enough to look up basic profile fields tenant-wide without granting group/device/directory-object read.</summary>
    public async Task<IReadOnlyList<TenantDirectoryUser>> SearchTenantUsersAsync(string query, CancellationToken ct)
    {
        var client = graphClientFactory.CreateClient();
        var escaped = query.Replace("\"", "");

        var response = await client.Users.GetAsync(config =>
        {
            config.QueryParameters.Search = $"\"displayName:{escaped}\" OR \"mail:{escaped}\"";
            config.QueryParameters.Select = ["id", "displayName", "mail", "userPrincipalName"];
            config.QueryParameters.Top = 15;
            config.Headers.Add("ConsistencyLevel", "eventual");
        }, ct);

        return response?.Value?
            .Where(u => u.Id is not null)
            .Select(u => new TenantDirectoryUser(u.Id!, u.DisplayName ?? u.UserPrincipalName ?? u.Id!, u.Mail ?? u.UserPrincipalName))
            .ToList() ?? [];
    }
}
