using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.ExternalServices;

/// <summary>
/// Reuses the same Graph app registration/client-credentials flow already needed for admin password resets
/// (adds the Mail.Send application permission) - no second email service. Sends as a configured shared mailbox
/// (GraphAdmin:SendMailAsUser), since application-permission sendMail requires a specific sender mailbox the
/// app is authorized to send as.
/// </summary>
public class GraphEmailSender(GraphClientFactory graphClientFactory, IConfiguration configuration, ILogger<GraphEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        var senderMailbox = configuration["GraphAdmin:SendMailAsUser"]
            ?? throw new InvalidOperationException("Missing GraphAdmin:SendMailAsUser.");

        try
        {
            var client = graphClientFactory.CreateClient();
            await client.Users[senderMailbox].SendMail.PostAsync(new SendMailPostRequestBody
            {
                Message = new Message
                {
                    Subject = subject,
                    Body = new ItemBody { ContentType = BodyType.Html, Content = htmlBody },
                    ToRecipients = [new Recipient { EmailAddress = new EmailAddress { Address = toEmail } }],
                },
                SaveToSentItems = false,
            }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            // Email is best-effort - the in-app Notification row is the authoritative record; a Graph outage
            // must never break notification creation.
            logger.LogWarning(ex, "Failed to send notification email to {ToEmail}", toEmail);
        }
    }
}
