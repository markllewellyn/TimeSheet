using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>
/// The 6 reports (Time/Cost/Profit x Project/Client), each filterable by 7 days/1 month/1 year/custom range.
/// Returns IReportingService's plain Domain records directly rather than mirroring them into Contracts DTOs -
/// an intentional exception to the usual Contracts-only-over-the-wire convention, since these are already pure
/// read-model shapes with no EF/business-logic entanglement and mirroring them would be pure boilerplate.
/// Time-on-* is visible to any authenticated user (their own logged time); Cost/Profit expose internal rates
/// and margin, so those two are Admin-only.
/// </summary>
public class ReportsFunctions(IReportingService reporting, ICurrentUserAccessor currentUser)
{
    [Function("Reports_TimeOnProject")]
    public async Task<IActionResult> TimeOnProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reports/time-on-project")] HttpRequest req, CancellationToken ct)
    {
        currentUser.RequireUser();
        var (projectId, range, error) = ParseProjectRequest(req);
        if (error is not null) return error;
        return new OkObjectResult(await reporting.GetTimeOnProjectReportAsync(projectId, range, ct));
    }

    [Function("Reports_TimeOnClient")]
    public async Task<IActionResult> TimeOnClient(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reports/time-on-client")] HttpRequest req, CancellationToken ct)
    {
        currentUser.RequireUser();
        var (clientId, range, error) = ParseClientRequest(req);
        if (error is not null) return error;
        return new OkObjectResult(await reporting.GetTimeOnClientReportAsync(clientId, range, ct));
    }

    [Function("Reports_CostOnProject")]
    public async Task<IActionResult> CostOnProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reports/cost-on-project")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;
        var (projectId, range, error) = ParseProjectRequest(req);
        if (error is not null) return error;
        return new OkObjectResult(await reporting.GetCostOnProjectReportAsync(projectId, range, ct));
    }

    [Function("Reports_CostOnClient")]
    public async Task<IActionResult> CostOnClient(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reports/cost-on-client")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;
        var (clientId, range, error) = ParseClientRequest(req);
        if (error is not null) return error;
        return new OkObjectResult(await reporting.GetCostOnClientReportAsync(clientId, range, ct));
    }

    [Function("Reports_ProfitOnProject")]
    public async Task<IActionResult> ProfitOnProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reports/profit-on-project")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;
        var (projectId, range, error) = ParseProjectRequest(req);
        if (error is not null) return error;
        return new OkObjectResult(await reporting.GetProfitOnProjectReportAsync(projectId, range, ct));
    }

    [Function("Reports_ProfitOnClient")]
    public async Task<IActionResult> ProfitOnClient(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reports/profit-on-client")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;
        var (clientId, range, error) = ParseClientRequest(req);
        if (error is not null) return error;
        return new OkObjectResult(await reporting.GetProfitOnClientReportAsync(clientId, range, ct));
    }

    private static (int ProjectId, ReportDateRange Range, IActionResult? Error) ParseProjectRequest(HttpRequest req)
    {
        if (!int.TryParse(req.Query["projectId"], out var projectId))
        {
            return (0, null!, new BadRequestObjectResult(new { error = "projectId query parameter is required." }));
        }
        var (range, rangeError) = ParseRange(req);
        return (projectId, range!, rangeError);
    }

    private static (int ClientId, ReportDateRange Range, IActionResult? Error) ParseClientRequest(HttpRequest req)
    {
        if (!int.TryParse(req.Query["clientId"], out var clientId))
        {
            return (0, null!, new BadRequestObjectResult(new { error = "clientId query parameter is required." }));
        }
        var (range, rangeError) = ParseRange(req);
        return (clientId, range!, rangeError);
    }

    private static (ReportDateRange? Range, IActionResult? Error) ParseRange(HttpRequest req)
    {
        var presetRaw = req.Query["rangePreset"].ToString();
        if (!Enum.TryParse<ReportRangePreset>(presetRaw, ignoreCase: true, out var preset))
        {
            return (null, new BadRequestObjectResult(new { error = "rangePreset must be one of Last7Days, LastMonth, LastYear, Custom." }));
        }

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
        switch (preset)
        {
            case ReportRangePreset.Last7Days:
                return (new ReportDateRange(today.AddDays(-6), today, preset), null);
            case ReportRangePreset.LastMonth:
                return (new ReportDateRange(today.AddMonths(-1), today, preset), null);
            case ReportRangePreset.LastYear:
                return (new ReportDateRange(today.AddYears(-1), today, preset), null);
            default:
                if (!DateOnly.TryParse(req.Query["startDate"], out var start) || !DateOnly.TryParse(req.Query["endDate"], out var end))
                {
                    return (null, new BadRequestObjectResult(new { error = "startDate and endDate query parameters are required when rangePreset=Custom." }));
                }
                return (new ReportDateRange(start, end, preset), null);
        }
    }
}
