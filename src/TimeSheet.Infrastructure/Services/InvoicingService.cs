using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class InvoicingService(
    IInvoiceRepository invoices,
    IClientRepository clients,
    IInvoiceGenerationService generation,
    IPdfInvoiceRenderer pdfRenderer,
    INotificationService notificationService,
    IUnitOfWork uow) : IInvoicingService
{
    public async Task<Invoice> GenerateDraftInvoiceAsync(int clientId, DateOnly periodStart, DateOnly periodEnd, decimal? manualExchangeRate, CancellationToken ct)
    {
        var freshDraft = await generation.BuildDraftAsync(clientId, periodStart, periodEnd, manualExchangeRate, ct);

        var existing = await invoices.GetDraftAsync(clientId, periodStart, ct);
        if (existing is not null)
        {
            // A Draft can be freely regenerated - nothing has gone to the client yet.
            invoices.ClearLineItems(existing);
            existing.PeriodEnd = periodEnd;
            existing.ReportingCurrency = freshDraft.ReportingCurrency;
            existing.ExchangeRate = freshDraft.ExchangeRate;
            existing.TotalAmount = freshDraft.TotalAmount;
            existing.GeneratedAtUtc = DateTimeOffset.UtcNow;
            foreach (var line in freshDraft.LineItems) existing.LineItems.Add(line);
            invoices.Update(existing);
            await uow.SaveChangesAsync(ct);
            return existing;
        }

        await invoices.AddAsync(freshDraft, ct);
        await uow.SaveChangesAsync(ct);
        return freshDraft;
    }

    public async Task<Invoice> FinalizeInvoiceAsync(int invoiceId, string invoiceNumber, int finalizedByUserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            throw new InvalidOperationException("An invoice number is required to finalize an invoice.");
        }

        var invoice = await invoices.GetByIdAsync(invoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} not found.");

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException("Only a Draft invoice can be finalized.");
        }

        if (await invoices.InvoiceNumberInUseAsync(invoice.ClientId, invoiceNumber, ct))
        {
            throw new InvalidOperationException($"Invoice number '{invoiceNumber}' is already in use for this client.");
        }

        var client = await clients.GetByIdAsync(invoice.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {invoice.ClientId} not found.");

        var document = new InvoiceDocumentModel(
            client.Name,
            FormatAddress(client),
            invoiceNumber,
            invoice.PeriodStart,
            invoice.PeriodEnd,
            invoice.ReportingCurrency,
            invoice.ExchangeRate,
            invoice.LineItems.Select(l => new InvoiceDocumentLine(l.Description, l.Hours, l.Amount)).ToList(),
            invoice.TotalAmount);

        invoice.PdfContent = await pdfRenderer.RenderAsync(document, ct);
        invoice.InvoiceNumber = invoiceNumber;
        invoice.Status = InvoiceStatus.Finalized;
        invoice.FinalizedAtUtc = DateTimeOffset.UtcNow;
        invoice.FinalizedByUserId = finalizedByUserId;

        invoices.Update(invoice);
        await uow.SaveChangesAsync(ct);

        await notificationService.RaiseToAdminsAsync(
            NotificationType.InvoiceGenerated,
            $"Invoice {invoiceNumber} for {client.Name} has been finalized.",
            NotificationChannel.InAppOnly, ct: ct);

        return invoice;
    }

    public async Task<byte[]> GetPdfAsync(int invoiceId, CancellationToken ct)
    {
        var invoice = await invoices.GetByIdAsync(invoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} not found.");

        if (invoice.Status != InvoiceStatus.Finalized || invoice.PdfContent is null)
        {
            throw new InvalidOperationException("The PDF is only available once the invoice has been finalized.");
        }

        return invoice.PdfContent;
    }

    private static string? FormatAddress(Client client)
    {
        var parts = new[] { client.BillingAddressLine1, client.BillingAddressLine2, client.BillingCity, client.BillingPostalCode, client.BillingCountryCode }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return parts.Any() ? string.Join(", ", parts) : null;
    }
}
