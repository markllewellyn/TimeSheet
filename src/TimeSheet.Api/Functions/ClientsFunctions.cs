using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Clients are business/billing entities (addresses, account managers, reporting currency) - all
/// endpoints are Admin-only per the plan's authorization invariants.</summary>
public class ClientsFunctions(
    IClientRepository clients, ICurrencyRepository currencies, IUnitOfWork uow, ICurrentUserAccessor currentUser, ILogger<ClientsFunctions> logger)
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

        var bodyError = await ValidateBodyAsync(body, ct);
        if (bodyError is not null) return bodyError;

        if (await clients.AccountCodeExistsAsync(body.AccountCode, null, ct))
        {
            return new ConflictObjectResult(new { error = $"Account code '{body.AccountCode}' is already in use." });
        }

        var (billingPeriod, periodStart, periodEnd) = ResolveBillingPeriod(body);

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
            BillingPeriod = billingPeriod,
            CurrentPeriodStart = periodStart,
            CurrentPeriodEnd = periodEnd,
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

        var bodyError = await ValidateBodyAsync(body, ct);
        if (bodyError is not null) return bodyError;

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

        var (billingPeriod, periodStart, periodEnd) = ResolveBillingPeriod(body);
        client.BillingPeriod = billingPeriod;
        client.CurrentPeriodStart = periodStart;
        client.CurrentPeriodEnd = periodEnd;

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

    /// <summary>Client.Name/AccountCode are backed by legacy fixed-width columns (40/50 chars - see
    /// ClientConfiguration) that would otherwise surface as a raw DB truncation error; CurrencyId, if supplied,
    /// must reference a real Currency row rather than silently creating a dangling FK.</summary>
    private async Task<IActionResult?> ValidateBodyAsync(UpsertClientRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
        {
            return new BadRequestObjectResult(new { error = "Name is required." });
        }
        if (body.Name.Length > 40)
        {
            return new BadRequestObjectResult(new { error = "Name cannot exceed 40 characters." });
        }
        if (string.IsNullOrWhiteSpace(body.AccountCode))
        {
            return new BadRequestObjectResult(new { error = "Account code is required." });
        }
        if (body.AccountCode.Length > 50)
        {
            return new BadRequestObjectResult(new { error = "Account code cannot exceed 50 characters." });
        }
        if (body.CurrencyId is { } currencyId && await currencies.GetByIdAsync(currencyId, ct) is null)
        {
            return new BadRequestObjectResult(new { error = "Invalid currency." });
        }
        return null;
    }

    /// <summary>Defaults BillingPeriod to OneOff when omitted/unrecognized (preserves today's manual-invoicing
    /// behavior for every existing client). Switching to Monthly with no period supplied defaults to the current
    /// calendar month rather than erroring - an admin flipping the flag shouldn't have to hand-compute a month
    /// boundary. Switching to (or staying) OneOff always clears both period fields, so no stale window lingers
    /// if a client is switched back later.</summary>
    private static (BillingPeriod BillingPeriod, DateOnly? PeriodStart, DateOnly? PeriodEnd) ResolveBillingPeriod(UpsertClientRequest body)
    {
        if (!Enum.TryParse<BillingPeriod>(body.BillingPeriod, out var billingPeriod) || billingPeriod != BillingPeriod.Monthly)
        {
            return (BillingPeriod.OneOff, null, null);
        }

        if (body.CurrentPeriodStart is { } start && body.CurrentPeriodEnd is { } end)
        {
            return (BillingPeriod.Monthly, start, end);
        }

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        return (BillingPeriod.Monthly, monthStart, monthEnd);
    }

    private static ClientDto ToDto(Client c) => new(
        c.Id, c.Name, c.AccountCode, c.StartDate,
        c.BillingAddressLine1, c.BillingAddressLine2, c.BillingCity, c.BillingPostalCode, c.BillingCountryCode,
        c.PrimaryContactName, c.PrimaryContactEmail, c.PrimaryContactPhone,
        c.CurrencyId, c.ReportingCurrencyCode, c.InvoicingMonthEndDay, c.Notes, c.IsActive,
        c.BillingPeriod.ToString(), c.CurrentPeriodStart, c.CurrentPeriodEnd);
}
