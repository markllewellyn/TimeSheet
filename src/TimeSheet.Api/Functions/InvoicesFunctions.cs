using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Generating/finalizing/browsing-by-client is Admin-only - a billing action. The one exception is
/// Invoices_ListForProjectManager, a read-only view scoped to a project manager's own project(s) - see
/// AuthorizationExtensions.RequireAdminOrProjectManager and the FDD's "project managers can view the staged
/// invoice for their projects".</summary>
public class InvoicesFunctions(
    IInvoiceRepository invoiceRepository,
    IProjectRepository projectRepository,
    IInvoicingService invoicing,
    IAuditLogService auditLog,
    ICurrentUserAccessor currentUser)
{
    [Function("Invoices_ListForProjectManager")]
    public async Task<IActionResult> ListForProjectManager(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "invoices/for-project-manager")] HttpRequest req, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var managedProjects = await projectRepository.GetManagedByUserAsync(user.UserId, ct);
        if (managedProjects.Count == 0) return new OkObjectResult(Array.Empty<ProjectManagerInvoiceDto>());

        var managedProjectIds = managedProjects.Select(p => p.Id).ToHashSet();
        var invoices = await invoiceRepository.GetByProjectIdsAsync(managedProjectIds, ct);

        var result = invoices.Select(i =>
        {
            var myLines = i.LineItems.Where(l => managedProjectIds.Contains(l.ProjectId)).ToList();
            return new ProjectManagerInvoiceDto(
                i.Id, i.ClientId, i.Client?.Name ?? "", i.PeriodStart, i.PeriodEnd,
                i.ReportingCurrency, i.Status.ToString(), i.InvoiceNumber, myLines.Sum(l => l.Amount),
                i.GeneratedAtUtc, i.FinalizedAtUtc,
                myLines.Select(ToLineItemDto).ToList());
        });
        return new OkObjectResult(result);
    }

    [Function("Invoices_ListByClient")]
    public async Task<IActionResult> ListByClient(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clients/{clientId:int}/invoices")] HttpRequest req, int clientId, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var result = await invoiceRepository.GetByClientAsync(clientId, ct);
        return new OkObjectResult(result.Select(i => ToDto(i, i.Client?.Name ?? "")));
    }

    [Function("Invoices_GenerateDraft")]
    public async Task<IActionResult> GenerateDraft(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "clients/{clientId:int}/invoices/draft")] HttpRequest req, int clientId, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var body = await req.ReadFromJsonAsync<GenerateDraftInvoiceRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        try
        {
            var invoice = await invoicing.GenerateDraftInvoiceAsync(clientId, body.PeriodStart, body.PeriodEnd, body.ManualExchangeRate, ct);
            var full = await invoiceRepository.GetByIdAsync(invoice.Id, ct);
            return new OkObjectResult(ToDto(full!, full!.Client?.Name ?? ""));
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
    }

    [Function("Invoices_Finalize")]
    public async Task<IActionResult> Finalize(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "invoices/{id:int}/finalize")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var body = await req.ReadFromJsonAsync<FinalizeInvoiceRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        try
        {
            var invoice = await invoicing.FinalizeInvoiceAsync(id, body.InvoiceNumber, currentUser.RequireUser().UserId, ct);
            var full = await invoiceRepository.GetByIdAsync(invoice.Id, ct);
            return new OkObjectResult(ToDto(full!, full!.Client?.Name ?? ""));
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
    }

    [Function("Invoices_ApplyLineItemDiscount")]
    public async Task<IActionResult> ApplyLineItemDiscount(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "invoices/{id:int}/line-items/{lineItemId:int}/discount")] HttpRequest req, int id, int lineItemId, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var body = await req.ReadFromJsonAsync<ApplyLineItemDiscountRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        try
        {
            var invoice = await invoicing.ApplyLineItemDiscountAsync(id, lineItemId, body.DiscountPercent, ct);
            var full = await invoiceRepository.GetByIdAsync(invoice.Id, ct);
            return new OkObjectResult(ToDto(full!, full!.Client?.Name ?? ""));
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
    }

    /// <summary>Bulk sibling of Invoices_ApplyLineItemDiscount - applies one discount to every line for a
    /// project on this invoice at once, since generation now produces one line per entry instead of one per
    /// project.</summary>
    [Function("Invoices_ApplyProjectDiscount")]
    public async Task<IActionResult> ApplyProjectDiscount(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "invoices/{id:int}/projects/{projectId:int}/discount")] HttpRequest req, int id, int projectId, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var body = await req.ReadFromJsonAsync<ApplyLineItemDiscountRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        try
        {
            var invoice = await invoicing.ApplyProjectDiscountAsync(id, projectId, body.DiscountPercent, ct);
            var full = await invoiceRepository.GetByIdAsync(invoice.Id, ct);
            return new OkObjectResult(ToDto(full!, full!.Client?.Name ?? ""));
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
    }

    /// <summary>Nothing was ever locked for a Draft, so this is a plain delete - no entries to unwind.</summary>
    [Function("Invoices_DeleteDraft")]
    public async Task<IActionResult> DeleteDraft(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "invoices/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var invoice = await invoiceRepository.GetByIdAsync(id, ct);
        if (invoice is null) return new NotFoundResult();

        try
        {
            await invoicing.DeleteDraftAsync(id, ct);
            await auditLog.LogAsync(currentUser.RequireUser(), "Invoice.DraftDeleted", "Invoice", id,
                $"{invoice.PeriodStart:yyyy-MM-dd} to {invoice.PeriodEnd:yyyy-MM-dd}, client {invoice.ClientId}", null, ct);
            return new NoContentResult();
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
    }

    /// <summary>Keeps the invoice, its real number and its PDF permanently - unlocks every entry/expense that
    /// was locked to it so they become invoiceable again. See InvoicingService.VoidInvoiceAsync.</summary>
    [Function("Invoices_Void")]
    public async Task<IActionResult> Void(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "invoices/{id:int}/void")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var body = await req.ReadFromJsonAsync<VoidInvoiceRequest>(ct) ?? new VoidInvoiceRequest(null);
        var user = currentUser.RequireUser();

        try
        {
            var invoice = await invoicing.VoidInvoiceAsync(id, body.Reason, user.UserId, user.DisplayName, ct);
            await auditLog.LogAsync(user, "Invoice.Voided", "Invoice", id, body.Reason, null, ct);
            var full = await invoiceRepository.GetByIdAsync(invoice.Id, ct);
            return new OkObjectResult(ToDto(full!, full!.Client?.Name ?? ""));
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
    }

    [Function("Invoices_Pdf")]
    public async Task<IActionResult> Pdf(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "invoices/{id:int}/pdf")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        try
        {
            var pdfStream = await invoicing.GetPdfAsync(id, ct);
            return new FileStreamResult(pdfStream, "application/pdf") { FileDownloadName = $"invoice-{id}.pdf" };
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
    }

    private static InvoiceDto ToDto(Invoice i, string clientName) => new(
        i.Id, i.ClientId, clientName, i.PeriodStart, i.PeriodEnd, i.ReportingCurrency, i.ExchangeRate, i.Status.ToString(),
        i.InvoiceNumber, i.TotalAmount, i.GeneratedAtUtc, i.FinalizedAtUtc,
        i.LineItems.Select(ToLineItemDto).ToList(),
        i.VoidedAtUtc, i.VoidedByName, i.VoidReason);

    private static InvoiceLineItemDto ToLineItemDto(InvoiceLineItem l) => new(
        l.Id, l.ProjectId, l.Project?.Name ?? "", l.Description, l.Hours, l.GrossAmount, l.DiscountPercent, l.Amount, l.Type.ToString(),
        l.StaffId, l.StaffName, l.TaskDate, l.Rate);
}
