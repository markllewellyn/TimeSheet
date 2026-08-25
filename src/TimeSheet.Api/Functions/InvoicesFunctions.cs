using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Invoicing is Admin-only throughout - generating/finalizing a client invoice is a billing action.</summary>
public class InvoicesFunctions(IInvoiceRepository invoiceRepository, IInvoicingService invoicing, ICurrentUserAccessor currentUser)
{
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
            var invoice = await invoicing.GenerateDraftInvoiceAsync(clientId, body.PeriodStart, body.PeriodEnd, ct);
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

    [Function("Invoices_Pdf")]
    public async Task<IActionResult> Pdf(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "invoices/{id:int}/pdf")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        try
        {
            var pdfBytes = await invoicing.GetPdfAsync(id, ct);
            return new FileContentResult(pdfBytes, "application/pdf") { FileDownloadName = $"invoice-{id}.pdf" };
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
    }

    private static InvoiceDto ToDto(Invoice i, string clientName) => new(
        i.Id, i.ClientId, clientName, i.PeriodStart, i.PeriodEnd, i.ReportingCurrency, i.Status.ToString(),
        i.InvoiceNumber, i.TotalAmount, i.GeneratedAtUtc, i.FinalizedAtUtc,
        i.LineItems.Select(l => new InvoiceLineItemDto(l.Id, l.ProjectId, l.Project?.Name ?? "", l.Description, l.Hours, l.Amount, l.Type.ToString())).ToList());
}
