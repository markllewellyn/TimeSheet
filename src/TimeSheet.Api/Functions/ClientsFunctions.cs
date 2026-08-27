using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Clients are business/billing entities (addresses, account managers, reporting currency) - all
/// endpoints are Admin-only per the plan's authorization invariants.</summary>
public class ClientsFunctions(IClientRepository clients, IUnitOfWork uow, ICurrentUserAccessor currentUser, ILogger<ClientsFunctions> logger)
{
    [Function("Clients_List")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clients")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var includeInactive = req.Query["includeInactive"] == "true";
        var result = await clients.GetAllAsync(includeInactive, ct);
        return new OkObjectResult(result.Select(ToDto));
    }

    [Function("Clients_Get")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clients/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var client = await clients.GetByIdAsync(id, ct);
        return client is null ? new NotFoundResult() : new OkObjectResult(ToDto(client));
    }

    [Function("Clients_Create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "clients")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var body = await req.ReadFromJsonAsync<UpsertClientRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        if (await clients.AccountCodeExistsAsync(body.AccountCode, null, ct))
        {
            return new ConflictObjectResult(new { error = $"Account code '{body.AccountCode}' is already in use." });
        }

        var client = new Client
        {
            Name = body.Name,
            AccountCode = body.AccountCode,
            StartDate = body.StartDate,
            BillingAddressLine1 = body.BillingAddressLine1,
            BillingAddressLine2 = body.BillingAddressLine2,
            BillingCity = body.BillingCity,
            BillingPostalCode = body.BillingPostalCode,
            BillingCountryCode = body.BillingCountryCode,
            PrimaryContactName = body.PrimaryContactName,
            PrimaryContactEmail = body.PrimaryContactEmail,
            PrimaryContactPhone = body.PrimaryContactPhone,
            CurrencyId = body.CurrencyId,
            InvoicingMonthEndDay = body.InvoicingMonthEndDay,
            Notes = body.Notes,
            CreatedUtc = DateTimeOffset.UtcNow,
            CreatedByUserId = currentUser.RequireUser().UserId,
        };

        await clients.AddAsync(client, ct);
        await uow.SaveChangesAsync(ct);

        logger.LogInformation("Client {ClientId} ({AccountCode}) created by user {UserId}", client.Id, client.AccountCode, currentUser.RequireUser().UserId);

        // Re-fetch so the Currency navigation (needed for ReportingCurrencyCode) is populated for the response.
        var saved = await clients.GetByIdAsync(client.Id, ct);
        return new CreatedResult($"/api/clients/{client.Id}", ToDto(saved!));
    }

    [Function("Clients_Update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "clients/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var client = await clients.GetByIdAsync(id, ct);
        if (client is null) return new NotFoundResult();

        var body = await req.ReadFromJsonAsync<UpsertClientRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        if (body.AccountCode != client.AccountCode && await clients.AccountCodeExistsAsync(body.AccountCode, id, ct))
        {
            return new ConflictObjectResult(new { error = $"Account code '{body.AccountCode}' is already in use." });
        }

        client.Name = body.Name;
        client.AccountCode = body.AccountCode;
        client.StartDate = body.StartDate;
        client.BillingAddressLine1 = body.BillingAddressLine1;
        client.BillingAddressLine2 = body.BillingAddressLine2;
        client.BillingCity = body.BillingCity;
        client.BillingPostalCode = body.BillingPostalCode;
        client.BillingCountryCode = body.BillingCountryCode;
        client.PrimaryContactName = body.PrimaryContactName;
        client.PrimaryContactEmail = body.PrimaryContactEmail;
        client.PrimaryContactPhone = body.PrimaryContactPhone;
        client.CurrencyId = body.CurrencyId;
        client.InvoicingMonthEndDay = body.InvoicingMonthEndDay;
        client.Notes = body.Notes;
        client.ModifiedUtc = DateTimeOffset.UtcNow;
        client.ModifiedByUserId = currentUser.RequireUser().UserId;

        clients.Update(client);
        await uow.SaveChangesAsync(ct);

        // Re-fetch so the Currency navigation reflects any CurrencyId change for the response.
        var saved = await clients.GetByIdAsync(id, ct);
        return new OkObjectResult(ToDto(saved!));
    }

    [Function("Clients_Deactivate")]
    public async Task<IActionResult> Deactivate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "clients/{id:int}/deactivate")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        // Clients are never hard-deleted once Projects/timesheets reference them - only soft-deactivated.
        var client = await clients.GetByIdAsync(id, ct);
        if (client is null) return new NotFoundResult();

        client.IsActive = false;
        client.ModifiedUtc = DateTimeOffset.UtcNow;
        client.ModifiedByUserId = currentUser.RequireUser().UserId;
        clients.Update(client);
        await uow.SaveChangesAsync(ct);
        return new NoContentResult();
    }

    private static ClientDto ToDto(Client c) => new(
        c.Id, c.Name, c.AccountCode, c.StartDate,
        c.BillingAddressLine1, c.BillingAddressLine2, c.BillingCity, c.BillingPostalCode, c.BillingCountryCode,
        c.PrimaryContactName, c.PrimaryContactEmail, c.PrimaryContactPhone,
        c.CurrencyId, c.ReportingCurrencyCode, c.InvoicingMonthEndDay, c.Notes, c.IsActive);
}
