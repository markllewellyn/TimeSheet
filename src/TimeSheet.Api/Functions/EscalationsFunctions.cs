using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Admin-only budget-overrun approval queue - approving/declining is always an explicit human decision.</summary>
public class EscalationsFunctions(IEscalationRepository escalations, IEscalationService escalationService, ICurrentUserAccessor currentUser)
{
    [Function("Escalations_Pending")]
    public async Task<IActionResult> Pending(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "escalations/pending")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var result = await escalations.GetPendingAsync(ct);
        return new OkObjectResult(result.Select(ToDto));
    }

    [Function("Escalations_Approve")]
    public async Task<IActionResult> Approve(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "escalations/{id:int}/approve")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var body = await req.ReadFromJsonAsync<DecisionRequest>(ct);
        var escalation = await escalationService.ApproveAsync(id, currentUser.RequireUser().UserId, body?.Notes, ct);
        return new OkObjectResult(ToDto(escalation));
    }

    [Function("Escalations_Decline")]
    public async Task<IActionResult> Decline(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "escalations/{id:int}/decline")] HttpRequest req, int id, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var body = await req.ReadFromJsonAsync<DecisionRequest>(ct);
        var escalation = await escalationService.DeclineAsync(id, currentUser.RequireUser().UserId, body?.Notes, ct);
        return new OkObjectResult(ToDto(escalation));
    }

    private static object ToDto(Escalation e) => new
    {
        e.Id,
        e.TimesheetEntryId,
        e.ProjectId,
        ProjectName = e.Project?.Name,
        ClientName = e.Project?.Client?.Name,
        Reason = e.Reason.ToString(),
        e.BudgetLimitAtTimeOfEntry,
        e.CumulativeValueAtTimeOfEntry,
        e.RaisedAtUtc,
        Decision = e.Decision.ToString(),
        e.DecidedByUserId,
        e.DecidedAtUtc,
        e.DecisionNotes,
    };
}

public record DecisionRequest(string? Notes);
