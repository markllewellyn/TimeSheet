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
}
