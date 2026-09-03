using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Admin-only - the health verdict surfaces internal budget/margin-adjacent signals.</summary>
public class ProjectHealthFunctions(IProjectHealthService projectHealth, ICurrentUserAccessor currentUser)
{
    [Function("ProjectHealth_Dashboard")]
    public async Task<IActionResult> Dashboard(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/health/dashboard")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;
        var result = await projectHealth.GetDashboardAsync(ct);
        return new OkObjectResult(result.Select(ToDto));
    }

    [Function("ProjectHealth_History")]
    public async Task<IActionResult> History(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{projectId:int}/health/history")] HttpRequest req, int projectId, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;
        var result = await projectHealth.GetHistoryAsync(projectId, ct);
        return new OkObjectResult(result.Select(ToDto));
    }

    /// <summary>Sweeps every active project on demand, same as the nightly timer - so an Admin can populate the
    /// dashboard's first assessments (or demo the feature) without waiting for 2am. Calls the exact same
    /// ReassessAllActiveAsync the timer calls (see ProjectHealthTimerFunction), so behavior is identical.</summary>
    [Function("ProjectHealth_RunNow")]
    public async Task<IActionResult> RunNow(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/health/run-now")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;
        var result = await projectHealth.ReassessAllActiveAsync(ct);
        return new OkObjectResult(result);
    }

    [Function("ProjectHealth_Reassess")]
    public async Task<IActionResult> Reassess(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects/{projectId:int}/health/reassess")] HttpRequest req, int projectId, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;
        var result = await projectHealth.ReassessAsync(projectId, ct);
        return new OkObjectResult(ToDto(result));
    }

    private static ProjectHealthAssessmentDto ToDto(ProjectHealthAssessment a) => new(
        a.Id, a.ProjectId, a.Project?.Name ?? "", a.Project?.Client?.Name ?? "", a.AssessedAtUtc,
        a.Status.ToString(), a.Summary,
        JsonSerializer.Deserialize<List<string>>(a.ContributingFactorsJson) ?? [],
        a.RecommendedAction, a.PercentBudgetConsumed, a.PercentTimeElapsed);
}
