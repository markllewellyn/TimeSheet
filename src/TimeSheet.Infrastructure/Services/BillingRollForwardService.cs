using Microsoft.Extensions.Logging;
using TimeSheet.Domain;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class BillingRollForwardService(
    IClientRepository clients,
    IInvoicingService invoicing,
    INotificationService notificationService,
    IUnitOfWork uow,
    ILogger<BillingRollForwardService> logger) : IBillingRollForwardService
{
    public async Task<BillingRollForwardResult> RunAsync(DateOnly asOfDate, CancellationToken ct)
    {
        var activeClients = await clients.GetAllAsync(includeInactive: false, ct);
        var due = activeClients
            .Where(c => c.BillingPeriod == BillingPeriod.Monthly && c.CurrentPeriodEnd is not null && c.CurrentPeriodEnd < asOfDate)
            .ToList();

        var generated = 0;
        var failed = 0;

        foreach (var client in due)
        {
            try
            {
                var periodStart = client.CurrentPeriodStart!.Value;
                var periodEnd = client.CurrentPeriodEnd!.Value;

                // GenerateDraftInvoiceAsync already saves internally - if the process crashes before this
                // method's own save below advances the period, the next run's CurrentPeriodEnd < asOfDate check
                // is still true, so it just regenerates the same still-Draft invoice for the same period again.
                // Safe, per GenerateDraftInvoiceAsync's existing "a Draft can be freely regenerated" guarantee -
                // no extra idempotency key needed.
                await invoicing.GenerateDraftInvoiceAsync(client.Id, periodStart, periodEnd, manualExchangeRate: null, ct);

                var nextStart = periodEnd.AddDays(1);
                var nextEnd = new DateOnly(nextStart.Year, nextStart.Month, DateTime.DaysInMonth(nextStart.Year, nextStart.Month));
                client.CurrentPeriodStart = nextStart;
                client.CurrentPeriodEnd = nextEnd;
                clients.Update(client);
                await uow.SaveChangesAsync(ct);

                await notificationService.RaiseToAdminsAsync(
                    NotificationType.InvoiceGenerated,
                    $"Recurring invoice draft generated for {client.Name}, period {periodStart:d} - {periodEnd:d}. Review in Invoicing.",
                    NotificationChannel.Both,
                    ct: ct);
                generated++;
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogWarning(ex, "Failed to roll forward billing period for client {ClientId}", client.Id);
            }
        }

        return new BillingRollForwardResult(due.Count, generated, failed);
    }
}
