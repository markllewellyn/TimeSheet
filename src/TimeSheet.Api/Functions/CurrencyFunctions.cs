using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Exposes currency conversion for ad-hoc UI use (e.g. previewing a figure in another currency).
/// Reporting/Invoicing consume ICurrencyConversionService directly rather than calling this over HTTP.</summary>
public class CurrencyFunctions(ICurrencyConversionService currencyConversion, ICurrencyRepository currencies, ICurrentUserAccessor currentUser)
{
    /// <summary>The legacy [Currency] master list - used to populate the currency picker on the Client
    /// create/edit form (Client.CurrencyId is a real FK into this table, not a plain ISO code).</summary>
    [Function("Currencies_List")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "currencies")] HttpRequest req, CancellationToken ct)
    {
        currentUser.RequireUser();
        var result = await currencies.GetAllAsync(ct);
        return new OkObjectResult(result.Select(c => new { c.Id, c.CurrencyName, c.CurrencyCode }));
    }

    [Function("Currency_Convert")]
    public async Task<IActionResult> Convert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "currency/convert")] HttpRequest req, CancellationToken ct)
    {
        currentUser.RequireUser();

        if (!decimal.TryParse(req.Query["amount"], out var amount) ||
            string.IsNullOrWhiteSpace(req.Query["from"]) ||
            string.IsNullOrWhiteSpace(req.Query["to"]))
        {
            return new BadRequestObjectResult(new { error = "amount, from, and to query parameters are required." });
        }

        var from = req.Query["from"].ToString().ToUpperInvariant();
        var to = req.Query["to"].ToString().ToUpperInvariant();
        var asOf = req.Query.TryGetValue("asOf", out var v) && DateOnly.TryParse(v, out var parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);

        var result = await currencyConversion.ConvertAsync(new Money(amount, from), to, asOf, ct);
        return new OkObjectResult(new { amount = result.Amount, currency = result.Currency, asOf });
    }
}
