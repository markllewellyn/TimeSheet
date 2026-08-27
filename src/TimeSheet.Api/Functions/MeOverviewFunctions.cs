using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>The legacy app's personal "Staff Overview" screen - a per-project breakdown of the caller's own
/// hours/payroll status. Reuses ITimesheetEntryRepository.GetForUserAsync (already fetches everything needed
/// with Client/Project included) rather than a dedicated aggregate query - a single user's timesheet history is
/// bounded, so grouping in memory here is simpler than adding a new SQL aggregate for it.</summary>
public class MeOverviewFunctions(ITimesheetEntryRepository entries, ICurrentUserAccessor currentUser)
{
    [Function("Me_Overview")]
    public async Task<IActionResult> Overview(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me/overview")] HttpRequest req, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var all = await entries.GetForUserAsync(user.UserId, null, null, null, ct);

        var lines = all
            .GroupBy(e => (e.ClientId, ClientName: e.Client?.Name ?? "", e.ProjectId, ProjectName: e.Project?.Name ?? ""))
            .Select(g => new MyOverviewLineDto(
                g.Key.ClientId, g.Key.ClientName, g.Key.ProjectId, g.Key.ProjectName,
                g.Sum(e => e.WorkHours), g.Sum(e => e.OutOfHoursHours), g.Sum(e => e.ToPayroll),
                g.All(e => e.SentToPayroll)))
            .OrderBy(l => l.ClientName).ThenBy(l => l.ProjectName)
            .ToList();

        return new OkObjectResult(lines);
    }
}
