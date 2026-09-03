using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Nightly assessment of every active project - a thin wrapper over IProjectHealthService
/// .ReassessAllActiveAsync, the same method the manual "Run Now" trigger calls (see
/// ProjectHealthFunctions.RunNow), so the two are guaranteed to behave identically.</summary>
public class ProjectHealthTimerFunction(IProjectHealthService projectHealth, ILogger<ProjectHealthTimerFunction> logger)
{
    [Function("NightlyProjectHealthAssessment")]
    public async Task Run([TimerTrigger("0 0 2 * * *")] TimerInfo timer, CancellationToken ct)
    {
        var result = await projectHealth.ReassessAllActiveAsync(ct);
        logger.LogInformation("Nightly project health sweep complete: {Assessed} assessed, {Failed} failed.", result.Assessed, result.Failed);
    }
}
