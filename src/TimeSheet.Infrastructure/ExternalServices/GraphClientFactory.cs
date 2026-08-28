using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;

namespace TimeSheet.Infrastructure.ExternalServices;

/// <summary>Shared builder for the Graph-admin app registration's client - used by GraphEmailSender
/// (Mail.Send), GraphAdminUserService.ForcePasswordResetAsync (User-PasswordProfile.ReadWrite.All), and
/// GraphAdminUserService.SearchTenantUsersAsync (User.Read.All - tenant directory lookup for the "enable/
/// disable any account in the tenant" FDD requirement), the same client-credentials app registration serving
/// all three purposes (see the plan's Authentication section). All three permissions need admin consent
/// granted on this app registration in the Entra tenant before the corresponding feature works at runtime.</summary>
public class GraphClientFactory(IConfiguration configuration)
{
    public GraphServiceClient CreateClient()
    {
        var tenantId = configuration["GraphAdmin:TenantId"] ?? throw new InvalidOperationException("Missing GraphAdmin:TenantId.");
        var clientId = configuration["GraphAdmin:ClientId"] ?? throw new InvalidOperationException("Missing GraphAdmin:ClientId.");
        var clientSecret = configuration["GraphAdmin:ClientSecret"] ?? throw new InvalidOperationException("Missing GraphAdmin:ClientSecret.");

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        return new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
    }
}
