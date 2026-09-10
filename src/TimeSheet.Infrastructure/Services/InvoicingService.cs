using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class InvoicingService(
    IInvoiceRepository invoices,
    IClientRepository clients,
    ITimesheetEntryRepository entries,
    IInvoiceGenerationService generation,
    IPdfInvoiceRenderer pdfRenderer,
    INotificationService notificationService,
    IUnitOfWork uow,
    IFileStorageService fileStorage) : IInvoicingService
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

    public async Task<Invoice> ApplyLineItemDiscountAsync(int invoiceId, int lineItemId, decimal? discountPercent, CancellationToken ct)
    {
        if (discountPercent is { } discount && (discount < 0 || discount > 100))
        {
            throw new InvalidOperationException("Discount must be between 0 and 100.");
        }

        var invoice = await invoices.GetByIdAsync(invoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} not found.");

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException("A line item discount can only be applied while the invoice is a Draft.");
        }

        var line = invoice.LineItems.FirstOrDefault(l => l.Id == lineItemId)
            ?? throw new InvalidOperationException($"Line item {lineItemId} not found on this invoice.");

        line.DiscountPercent = discountPercent;
        line.Amount = discountPercent is { } d ? Math.Round(line.GrossAmount * (1 - d / 100m), 2) : line.GrossAmount;
        invoice.TotalAmount = Math.Round(invoice.LineItems.Sum(l => l.Amount), 2);

        invoices.Update(invoice);
        await uow.SaveChangesAsync(ct);
        return invoice;
    }

    public async Task<Invoice> ApplyProjectDiscountAsync(int invoiceId, int projectId, decimal? discountPercent, CancellationToken ct)
    {
        if (discountPercent is { } discount && (discount < 0 || discount > 100))
        {
            throw new InvalidOperationException("Discount must be between 0 and 100.");
        }

        var invoice = await invoices.GetByIdAsync(invoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} not found.");

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException("A line item discount can only be applied while the invoice is a Draft.");
        }

        var lines = invoice.LineItems.Where(l => l.ProjectId == projectId).ToList();
        if (lines.Count == 0)
        {
            throw new InvalidOperationException($"No line items for project {projectId} on this invoice.");
        }

        foreach (var line in lines)
        {
            line.DiscountPercent = discountPercent;
            line.Amount = discountPercent is { } d ? Math.Round(line.GrossAmount * (1 - d / 100m), 2) : line.GrossAmount;
        }
        invoice.TotalAmount = Math.Round(invoice.LineItems.Sum(l => l.Amount), 2);

        invoices.Update(invoice);
        await uow.SaveChangesAsync(ct);
        return invoice;
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
            invoice.LineItems.Select(l => new InvoiceDocumentLine(
                l.Project?.Name ?? "", l.StaffName, l.TaskDate, l.Description, l.Hours, l.Rate, l.Amount)).ToList(),
            invoice.TotalAmount);

        var pdfBytes = await pdfRenderer.RenderAsync(document, ct);
        using var pdfStream = new MemoryStream(pdfBytes);
        invoice.PdfStorageKey = await fileStorage.SaveAsync($"invoice-{invoice.Id}.pdf", pdfStream, ct);
        invoice.InvoiceNumber = invoiceNumber;
        invoice.Status = InvoiceStatus.Finalized;
        invoice.FinalizedAtUtc = DateTimeOffset.UtcNow;
        invoice.FinalizedByUserId = finalizedByUserId;

        invoices.Update(invoice);
        await LockEntriesAsync(invoice, ct);
        await uow.SaveChangesAsync(ct);

        await notificationService.RaiseToAdminsAsync(
            NotificationType.InvoiceGenerated,
            $"Invoice {invoiceNumber} for {client.Name} has been finalized.",
            NotificationChannel.InAppOnly, ct: ct);

        return invoice;
    }

    public async Task<Stream> GetPdfAsync(int invoiceId, CancellationToken ct)
    {
        var invoice = await invoices.GetByIdAsync(invoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} not found.");

        if (invoice.Status != InvoiceStatus.Finalized || invoice.PdfStorageKey is null)
        {
            throw new InvalidOperationException("The PDF is only available once the invoice has been finalized.");
        }

        return await fileStorage.OpenReadAsync(invoice.PdfStorageKey, ct);
    }

    /// <summary>FDD: "Finalizing an invoice locks the entries it was built from." InvoiceLineItem has no FK
    /// back to the TimesheetEntry it came from (only a StaffId/TaskDate/Description snapshot) - so the entry
    /// set is reconstituted here by re-running the exact same period/project query that built the line items in
    /// the first place, then stamping each one with this invoice's id. Fixed Fee and Expense line items don't
    /// derive from TimesheetEntry rows, so there's nothing to lock for those. Idempotent-safe to call more than
    /// once (an already-locked entry is simply re-stamped with the same value), though FinalizeInvoiceAsync's
    /// Draft-only guard means that never actually happens.</summary>
    private async Task LockEntriesAsync(Invoice invoice, CancellationToken ct)
    {
        // DistinctBy(ProjectId) - there's now one InvoiceLineItem per entry rather than one per project, so
        // without this the identical GetCountedForInvoicingAsync query would otherwise re-run once per entry.
        foreach (var line in invoice.LineItems.Where(l => l.Type == InvoiceLineItemType.TimeAndMaterials).DistinctBy(l => l.ProjectId))
        {
            var counted = await entries.GetCountedForInvoicingAsync(line.ProjectId, invoice.PeriodStart, invoice.PeriodEnd, ct);
            foreach (var entry in counted)
            {
                entry.InvoiceId = invoice.Id;
                entries.Update(entry);
            }
        }
    }

    private static string? FormatAddress(Client client)
    {
        var parts = new[] { client.BillingAddressLine1, client.BillingAddressLine2, client.BillingCity, client.BillingPostalCode, client.BillingCountryCode }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return parts.Any() ? string.Join(", ", parts) : null;
    }
}
