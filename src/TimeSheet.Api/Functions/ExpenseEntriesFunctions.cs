using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

public class ExpenseEntriesFunctions(
    IExpenseEntryRepository expenses,
    IStaffProjectRepository assignments,
    IProjectRepository projects,
    IAuditLogService auditLog,
    IUnitOfWork uow,
    ICurrentUserAccessor currentUser)
{
    [Function("ExpenseEntries_List")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "expense-entries")] HttpRequest req, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var search = req.Query["search"].ToString();
        var from = req.Query.TryGetValue("from", out var f) && DateOnly.TryParse(f, out var fd) ? fd : (DateOnly?)null;
        var to = req.Query.TryGetValue("to", out var t) && DateOnly.TryParse(t, out var td) ? td : (DateOnly?)null;

        var result = await expenses.GetForUserAsync(user.UserId, search, from, to, ct);
        return new OkObjectResult(result.Select(ToDto));
    }

    [Function("ExpenseEntries_Create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "expense-entries")] HttpRequest req, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var body = await req.ReadFromJsonAsync<CreateExpenseEntryRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        if (!Enum.TryParse<ExpenseEntryKind>(body.Kind ?? nameof(ExpenseEntryKind.Expense), out var kind))
        {
            return new BadRequestObjectResult(new { error = "Invalid Kind." });
        }

        // "Contract" (an Admin-only monetary value against a non-invoiceable project) skips the normal
        // project-assignment check entirely - it isn't tied to the admin's own work on the project, so
        // requiring them to be personally assigned would be the wrong gate. CanInvoice + Admin role is the
        // actual control here, mirroring the legacy Power App's separate admin-only value-entry flow.
        if (kind == ExpenseEntryKind.Contract)
        {
            if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

            var contractProject = await projects.GetByIdAsync(body.ProjectId, ct);
            if (contractProject is null) return new NotFoundObjectResult(new { error = "Project not found." });
            if (contractProject.CanInvoice == true)
            {
                return new BadRequestObjectResult(new { error = "Contract values can only be logged against projects that cannot be invoiced." });
            }
        }
        else
        {
            var validation = await ValidateAssignmentAsync(user.UserId, body.ProjectId, body.Date, ct);
            if (validation is not null) return validation;
        }

        var entry = new ExpenseEntry
        {
            UserId = user.UserId,
            ProjectId = body.ProjectId,
            Kind = kind,
            Date = body.Date,
            Amount = body.Amount,
            Currency = body.Currency,
            Description = body.Description,
            IsBillable = body.IsBillable,
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        await expenses.AddAsync(entry, ct);
        await uow.SaveChangesAsync(ct);

        // Two-step save - EntityId needs the entry's generated Id, only assigned once the entry's own save above
        // has run.
        await auditLog.LogAsync(user, "ExpenseEntry.Created", "ExpenseEntry", entry.Id,
            $"{entry.Date:yyyy-MM-dd}, {entry.Amount} {entry.Currency} ({kind})", impersonatedUserId: null, ct);
        await uow.SaveChangesAsync(ct);

        var saved = await expenses.GetByIdAsync(entry.Id, ct);
        return new CreatedResult($"/api/expense-entries/{entry.Id}", ToDto(saved!));
    }

    [Function("ExpenseEntries_Update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "expense-entries/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var entry = await expenses.GetByIdAsync(id, ct);
        if (entry is null || entry.UserId != user.UserId) return new NotFoundResult();

        var body = await req.ReadFromJsonAsync<UpdateExpenseEntryRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        // Mirrors Create's kind-based gate: a Contract entry was never subject to the assignment check to
        // begin with, so editing one must not suddenly require it either.
        if (entry.Kind != ExpenseEntryKind.Contract)
        {
            var validation = await ValidateAssignmentAsync(user.UserId, entry.ProjectId, body.Date, ct);
            if (validation is not null) return validation;
        }

        entry.Date = body.Date;
        entry.Amount = body.Amount;
        entry.Currency = body.Currency;
        entry.Description = body.Description;
        entry.IsBillable = body.IsBillable;
        entry.ModifiedUtc = DateTimeOffset.UtcNow;

        expenses.Update(entry);
        await auditLog.LogAsync(user, "ExpenseEntry.Updated", "ExpenseEntry", entry.Id,
            $"{entry.Date:yyyy-MM-dd}, {entry.Amount} {entry.Currency}", impersonatedUserId: null, ct);
        await uow.SaveChangesAsync(ct);
        return new OkObjectResult(ToDto(entry));
    }

    [Function("ExpenseEntries_Delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "expense-entries/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var entry = await expenses.GetByIdAsync(id, ct);
        if (entry is null || entry.UserId != user.UserId) return new NotFoundResult();

        expenses.Remove(entry);
        await auditLog.LogAsync(user, "ExpenseEntry.Deleted", "ExpenseEntry", entry.Id,
            $"{entry.Date:yyyy-MM-dd}, {entry.Amount} {entry.Currency}", impersonatedUserId: null, ct);
        await uow.SaveChangesAsync(ct);
        return new NoContentResult();
    }

    private async Task<IActionResult?> ValidateAssignmentAsync(int userId, int projectId, DateOnly date, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(projectId, ct);
        if (project is null) return new NotFoundObjectResult(new { error = "Project not found." });

        if (!await assignments.IsUserAssignedAsync(userId, projectId, date, ct))
        {
            return new ObjectResult(new { error = "You are not assigned to this project for the given date." })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
        }
        return null;
    }

    private static ExpenseEntryDto ToDto(ExpenseEntry e) => new(
        e.Id, e.ProjectId, e.Project?.Name ?? "", e.Project?.ClientId ?? 0, e.Project?.Client?.Name ?? "",
        e.Date, e.Amount, e.Currency, e.Description, e.IsBillable, e.Kind.ToString());
}
