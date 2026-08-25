using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;

namespace TimeSheet.Infrastructure.ExternalServices;

/// <summary>Shared builder for the Graph-admin app registration's client - used by both GraphEmailSender
/// (Mail.Send) and GraphAdminUserService (User-PasswordProfile.ReadWrite.All), the same client-credentials
/// app registration serving both purposes (see the plan's Authentication section).</summary>
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
