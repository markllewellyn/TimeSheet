using System.Security.Cryptography;
using Microsoft.Graph.Models;
using TimeSheet.Domain.Services;

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
        var temporaryPassword = GenerateTemporaryPassword();
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

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%^&*";
        const string all = upper + lower + digits + symbols;

        // Guarantee at least one of each character class, then fill the rest randomly - meets typical Entra
        // tenant password complexity policies (upper/lower/digit/symbol, 12+ characters). Shuffled (not just
        // appended) so the guaranteed characters aren't always in the same predictable positions.
        var chars = new[]
        {
            PickRandom(upper), PickRandom(lower), PickRandom(digits), PickRandom(symbols),
        }.Concat(Enumerable.Range(0, 8).Select(_ => PickRandom(all))).ToArray();

        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    private static char PickRandom(string source) => source[RandomNumberGenerator.GetInt32(source.Length)];
}
