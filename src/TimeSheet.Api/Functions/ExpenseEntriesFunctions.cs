using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using static TimeSheet.Api.Auth.ImpersonationAuthorization;
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
    IUserRepository users,
    IAuditLogService auditLog,
    IUnitOfWork uow,
    ICurrentUserAccessor currentUser)
{
    [Function("ExpenseEntries_GetById")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "expense-entries/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var entry = await expenses.GetByIdAsync(id, ct);
        if (entry is null) return new NotFoundResult();
        var (authorized, impersonatedUserId) = CheckOwnership(entry.UserId, user, ParseOnBehalfOfUserId(req));
        if (!authorized) return new NotFoundResult();
        if (impersonatedUserId is { } impId && await ValidateImpersonationTargetAsync(users, impId, ct) is { } impError) return impError;
        return new OkObjectResult(ToDto(entry));
    }

    [Function("ExpenseEntries_List")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "expense-entries")] HttpRequest req, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var (viewError, effectiveUserId) = await ResolveViewTargetAsync(users, user, ParseOnBehalfOfUserId(req), ct);
        if (viewError is not null) return viewError;

        var search = req.Query["search"].ToString();
        var from = req.Query.TryGetValue("from", out var f) && DateOnly.TryParse(f, out var fd) ? fd : (DateOnly?)null;
        var to = req.Query.TryGetValue("to", out var t) && DateOnly.TryParse(t, out var td) ? td : (DateOnly?)null;

        var result = await expenses.GetForUserAsync(effectiveUserId, search, from, to, ct);
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

        var effectiveUserId = user.UserId;
        int? impersonatedUserId = null;

        // "Contract" (an Admin-only monetary value against a non-invoiceable project) skips the normal
        // project-assignment check entirely - it isn't tied to the admin's own work on the project, so
        // requiring them to be personally assigned would be the wrong gate. CanInvoice + Admin role is the
        // actual control here, mirroring the legacy Power App's separate admin-only value-entry flow. It's also
        // never logged "on behalf of" anyone - it's the Admin's own value entry, not tied to a staff member.
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
            // Impersonation: an Admin logging an expense on behalf of someone else - mirrors
            // TimesheetEntriesFunctions.Create's identical check.
            if (body.OnBehalfOfUserId is { } onBehalfOfUserId && onBehalfOfUserId != user.UserId)
            {
                if (!user.IsAdmin)
                {
                    return new ObjectResult(new { error = "Admin role required to log an expense on behalf of another user." })
                    {
                        StatusCode = StatusCodes.Status403Forbidden,
                    };
                }
                if (await ValidateImpersonationTargetAsync(users, onBehalfOfUserId, ct) is { } impersonationError) return impersonationError;
                effectiveUserId = onBehalfOfUserId;
                impersonatedUserId = onBehalfOfUserId;
            }

            var validation = await ValidateAssignmentAsync(effectiveUserId, body.ProjectId, body.Date, ct);
            if (validation is not null) return validation;
        }

        var entry = new ExpenseEntry
        {
            UserId = effectiveUserId,
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
            $"{entry.Date:yyyy-MM-dd}, {entry.Amount} {entry.Currency} ({kind})", impersonatedUserId, ct);
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
        if (entry is null) return new NotFoundResult();

        var body = await req.ReadFromJsonAsync<UpdateExpenseEntryRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        var (authorized, impersonatedUserId) = CheckOwnership(entry.UserId, user, body.OnBehalfOfUserId);
        if (!authorized) return new NotFoundResult();
        if (impersonatedUserId is { } impId && await ValidateImpersonationTargetAsync(users, impId, ct) is { } impError) return impError;
        if (entry.InvoiceId is not null) return InvoicedLockedResult();

        // Mirrors Create's kind-based gate: a Contract entry was never subject to the assignment check to
        // begin with, so editing one must not suddenly require it either.
        if (entry.Kind != ExpenseEntryKind.Contract)
        {
            var validation = await ValidateAssignmentAsync(entry.UserId, entry.ProjectId, body.Date, ct);
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
            $"{entry.Date:yyyy-MM-dd}, {entry.Amount} {entry.Currency}", impersonatedUserId, ct);
        await uow.SaveChangesAsync(ct);
        return new OkObjectResult(ToDto(entry));
    }

    [Function("ExpenseEntries_Delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "expense-entries/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var entry = await expenses.GetByIdAsync(id, ct);
        if (entry is null) return new NotFoundResult();
        var (authorized, impersonatedUserId) = CheckOwnership(entry.UserId, user, ParseOnBehalfOfUserId(req));
        if (!authorized) return new NotFoundResult();
        if (impersonatedUserId is { } impId && await ValidateImpersonationTargetAsync(users, impId, ct) is { } impError) return impError;
        if (entry.InvoiceId is not null) return InvoicedLockedResult();

        expenses.Remove(entry);
        await auditLog.LogAsync(user, "ExpenseEntry.Deleted", "ExpenseEntry", entry.Id,
            $"{entry.Date:yyyy-MM-dd}, {entry.Amount} {entry.Currency}", impersonatedUserId, ct);
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

    /// <summary>FDD: "Finalizing an invoice locks the entries it was built from." Set by
    /// InvoicingService.FinalizeInvoiceAsync, never here - this is read-only enforcement.</summary>
    private static IActionResult InvoicedLockedResult() =>
        new ObjectResult(new { error = "This entry has been included on a finalized invoice and can no longer be changed." })
        {
            StatusCode = StatusCodes.Status409Conflict,
        };

    private static ExpenseEntryDto ToDto(ExpenseEntry e) => new(
        e.Id, e.ProjectId, e.Project?.Name ?? "", e.Project?.ClientId ?? 0, e.Project?.Client?.Name ?? "",
        e.Date, e.Amount, e.Currency, e.Description, e.IsBillable, e.Kind.ToString(),
        e.Attachments.Select(a => new AttachmentDto(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedAtUtc)).ToList(),
        e.InvoiceId is not null);
}
