using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Nightly assessment of every active project. Bounded concurrency respects the Anthropic API's rate
/// limits rather than firing every project's call simultaneously.</summary>
public class ProjectHealthTimerFunction(IProjectRepository projects, IProjectHealthService projectHealth, ILogger<ProjectHealthTimerFunction> logger)
{
    private const int MaxConcurrency = 4;

    [Function("NightlyProjectHealthAssessment")]
    public async Task Run([TimerTrigger("0 0 2 * * *")] TimerInfo timer, CancellationToken ct)
    {
        var activeProjects = await projects.GetAllActiveAsync(ct);
        using var throttle = new SemaphoreSlim(MaxConcurrency);
        var assessed = 0;
        var failed = 0;

        var tasks = activeProjects.Select(async project =>
        {
            await throttle.WaitAsync(ct);
            try
            {
                await projectHealth.ReassessAsync(project.Id, ct);
                Interlocked.Increment(ref assessed);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failed);
                logger.LogWarning(ex, "Failed to assess health for project {ProjectId}", project.Id);
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks);
        logger.LogInformation("Nightly project health sweep complete: {Assessed} assessed, {Failed} failed.", assessed, failed);
    }
}
