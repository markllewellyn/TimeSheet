using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Monthly billing period roll-forward (FDD: "monthly recurring project billing and billing period
/// roll-forward"). Runs 30 minutes before MonthlyPayrollAggregation so the two never race each other.</summary>
public class BillingRollForwardTimerFunction(IBillingRollForwardService billingRollForward, ILogger<BillingRollForwardTimerFunction> logger)
{
    [Function("MonthlyBillingRollForward")]
    public async Task Run([TimerTrigger("0 0 3 1 * *")] TimerInfo timer, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
        var result = await billingRollForward.RunAsync(today, ct);
        logger.LogInformation("Monthly billing roll-forward complete: {Due} due, {Generated} generated, {Failed} failed.",
            result.ClientsDue, result.InvoicesGenerated, result.Failed);
    }
}
