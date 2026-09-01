using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Manual trigger for a demo/admin who doesn't want to wait for the 1st of the month - runs the
/// exact same roll-forward the MonthlyBillingRollForward timer does. Admin-only, like Invoicing/Clients (this
/// generates real Draft invoices and advances client billing windows, same blast radius as Invoices_GenerateDraft).</summary>
public class BillingRollForwardFunctions(IBillingRollForwardService billingRollForward, ICurrentUserAccessor currentUser)
{
    [Function("BillingRollForward_RunNow")]
    public async Task<IActionResult> RunNow(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "billing-roll-forward/run-now")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
        var result = await billingRollForward.RunAsync(today, ct);
        return new OkObjectResult(result);
    }
}
