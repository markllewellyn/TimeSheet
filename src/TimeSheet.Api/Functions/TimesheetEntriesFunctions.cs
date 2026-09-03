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

/// <summary>Timesheet entries are always scoped to the signed-in caller - a User (or Admin, for their own
/// personal timesheet) can only ever see/create/edit their own entries. Admin-wide visibility across all
/// users' timesheets is a separate concern (Reporting/Escalations), not this endpoint.</summary>
public class TimesheetEntriesFunctions(
    ITimesheetEntryRepository entries,
    IStaffProjectRepository assignments,
    IProjectRepository projects,
    IClientRepository clients,
    IEntryTypeRepository entryTypes,
    IRateResolver rateResolver,
    IBudgetMonitoringService budgetMonitoring,
    IEntryFlagService entryFlagService,
    INotificationService notificationService,
    IAuditLogService auditLog,
    IUnitOfWork uow,
    ICurrentUserAccessor currentUser)
{
    [Function("TimesheetEntries_List")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "timesheet-entries")] HttpRequest req, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var (viewError, effectiveUserId) = ResolveViewTarget(user, ParseOnBehalfOfUserId(req));
        if (viewError is not null) return viewError;

        var search = req.Query["search"].ToString();
        var from = req.Query.TryGetValue("from", out var f) && DateOnly.TryParse(f, out var fd) ? fd : (DateOnly?)null;
        var to = req.Query.TryGetValue("to", out var t) && DateOnly.TryParse(t, out var td) ? td : (DateOnly?)null;

        var result = await entries.GetForUserAsync(effectiveUserId, search, from, to, ct);
        var dtos = result.Select(ToDto).ToList();

        return new OkObjectResult(new
        {
            entries = dtos,
            summary = BuildSummary(result),
        });
    }

    [Function("TimesheetEntries_Get")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "timesheet-entries/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var entry = await entries.GetByIdAsync(id, ct);
        if (entry is null) return new NotFoundResult();
        var (authorized, _) = CheckOwnership(entry, user, ParseOnBehalfOfUserId(req));
        if (!authorized) return new NotFoundResult();
        return new OkObjectResult(ToDto(entry));
    }

    [Function("TimesheetEntries_Create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "timesheet-entries")] HttpRequest req, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var body = await req.ReadFromJsonAsync<CreateTimesheetEntryRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        if (string.IsNullOrWhiteSpace(body.Description))
        {
            return new BadRequestObjectResult(new { error = "Description is required." });
        }

        // Impersonation: an Admin logging time on behalf of someone else. effectiveUserId is whose timesheet
        // the entry lands on; createdByUserId (kept null in the normal self-logging case) records who actually
        // submitted it, so the two are never silently conflated - see TimesheetEntry.CreatedByUserId.
        var effectiveUserId = user.UserId;
        int? createdByUserId = null;
        if (body.OnBehalfOfUserId is { } onBehalfOfUserId && onBehalfOfUserId != user.UserId)
        {
            if (!user.IsAdmin)
            {
                return new ObjectResult(new { error = "Admin role required to log time on behalf of another user." })
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                };
            }
            effectiveUserId = onBehalfOfUserId;
            createdByUserId = user.UserId;
        }

        var (validationError, project) = await ValidateAssignmentAsync(effectiveUserId, body.ProjectId, body.Date, ct);
        if (validationError is not null) return validationError;

        var valuesError = await ValidateEntryValuesAsync(project!.ClientId, effectiveUserId, body.Date, body.WorkHours, body.OutOfHoursHours, excludeEntryId: null, ct);
        if (valuesError is not null) return valuesError;

        var entryTypeError = await ValidateEntryTypeAsync(body.EntryTypeId, body.ProjectId, project.ProjectType, ct);
        if (entryTypeError is not null) return entryTypeError;

        var (payrollError, amounts) = await ComputePayrollAmountsAsync(effectiveUserId, project!.ClientId, body.ProjectId, body.Date, body.WorkHours, body.OutOfHoursHours, ct);
        if (payrollError is not null) return payrollError;

        var entry = new TimesheetEntry
        {
            UserId = effectiveUserId,
            ClientId = project!.ClientId,
            ProjectId = body.ProjectId,
            Date = body.Date,
            WorkHours = body.WorkHours,
            OutOfHoursHours = body.OutOfHoursHours,
            Description = body.Description,
            EntryTypeId = body.EntryTypeId,
            ToPayroll = amounts!.ToPayroll,
            ToCompany = amounts.ToCompany,
            ResolvedCustomerRate = amounts.ResolvedCustomerRate,
            ResolvedHourlyCost = amounts.ResolvedHourlyCost,
            ResolvedOutOfHoursCost = amounts.ResolvedOutOfHoursCost,
            RateCardId = amounts.RateCardId,
            StaffCostId = amounts.StaffCostId,
            Tier = amounts.Tier,
            BillingPeriodChoice = ResolveBillingPeriodChoice(body.BillingPeriodChoice),
            CreatedByUserId = createdByUserId,
            CreatedUtc = DateTimeOffset.UtcNow,
        };

        // Budget check never gates the save (FDD: "nothing is blocked") - an over-budget entry is still saved
        // and counts normally immediately; it's flagged for query instead (see IEntryFlagService). Still runs
        // before the fast-track below purely so the notification/flag reflects the entry as actually saved.
        var budgetCheck = await budgetMonitoring.EvaluateAsync(entry, ct);
        if (body.AdminSendToPayroll == true && user.IsAdmin)
        {
            // Admin-only fast-track (mirrors the legacy app's Add-screen "Sent to Payroll" checkbox) - creates
            // the entry already approved and sent, bypassing the normal Approvals queue in one step.
            var now = DateTimeOffset.UtcNow;
            entry.ApprovedPayroll = true;
            entry.ApprovedByStaffId = user.UserId;
            entry.ApprovedByName = user.DisplayName;
            entry.DateApprovedPayroll = now;
            entry.SentToPayroll = true;
            entry.SentByStaffId = user.UserId;
            entry.SentByName = user.DisplayName;
            entry.DateSentToPayroll = now;
        }

        await entries.AddAsync(entry, ct);
        await uow.SaveChangesAsync(ct);

        // Two-step save: the audit row's EntityId needs the entry's generated Id, which SQLite only assigns
        // once the entry's own SaveChangesAsync above has actually run (mirrors how EntryFlag/notifications
        // already save as their own step after the entry itself, rather than one big atomic transaction).
        var impersonatedUserId = createdByUserId is not null ? effectiveUserId : (int?)null;
        await auditLog.LogAsync(user, "TimesheetEntry.Created", "TimesheetEntry", entry.Id,
            $"{entry.Date:yyyy-MM-dd}, {entry.WorkHours + entry.OutOfHoursHours}h on project {entry.ProjectId}", impersonatedUserId, ct);
        await uow.SaveChangesAsync(ct);

        if (budgetCheck.RequiresEscalation)
        {
            await entryFlagService.RaiseSystemAsync(entry, EntryFlagReason.ProjectBudgetExceeded, budgetCheck.Limit!.Value, budgetCheck.CumulativeValue, ct);
        }
        else if (budgetCheck.NewlyCrossedNotificationThresholdPercent is { } crossedPercent)
        {
            // Persist the crossing so the next entry on this project doesn't re-notify for the same
            // threshold - project is already tracked by this request's DbContext (loaded in
            // ValidateAssignmentAsync above), and RaiseToAdminsAsync's own SaveChangesAsync below flushes
            // this change too, so no separate save call is needed here.
            project!.HighestBudgetNotificationPercent = crossedPercent;
            projects.Update(project);
            await notificationService.RaiseToAdminsAsync(
                NotificationType.BudgetWarning,
                $"Project {project.Name} has reached {crossedPercent}% of its allotted {budgetCheck.Limit} hours ({budgetCheck.CumulativeValue}/{budgetCheck.Limit}).",
                NotificationChannel.InAppOnly, body.ProjectId, entry.Id, ct);
        }

        var saved = await entries.GetByIdAsync(entry.Id, ct);
        return new CreatedResult($"/api/timesheet-entries/{entry.Id}", ToDto(saved!));
    }

    [Function("TimesheetEntries_Update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "timesheet-entries/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var entry = await entries.GetByIdAsync(id, ct);
        if (entry is null) return new NotFoundResult();
        if (entry.ApprovedPayroll || entry.SentToPayroll) return SentToPayrollLockedResult();
        if (entry.InvoiceId is not null) return InvoicedLockedResult();

        var body = await req.ReadFromJsonAsync<UpdateTimesheetEntryRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        var (authorized, impersonatedUserId) = CheckOwnership(entry, user, body.OnBehalfOfUserId);
        if (!authorized) return new NotFoundResult();

        if (string.IsNullOrWhiteSpace(body.Description))
        {
            return new BadRequestObjectResult(new { error = "Description is required." });
        }

        var (validationError, _) = await ValidateAssignmentAsync(entry.UserId, entry.ProjectId, body.Date, ct);
        if (validationError is not null) return validationError;

        var valuesError = await ValidateEntryValuesAsync(entry.ClientId, entry.UserId, body.Date, body.WorkHours, body.OutOfHoursHours, excludeEntryId: entry.Id, ct);
        if (valuesError is not null) return valuesError;

        var entryTypeError = await ValidateEntryTypeAsync(body.EntryTypeId, entry.ProjectId, entry.Project!.ProjectType, ct);
        if (entryTypeError is not null) return entryTypeError;

        var (payrollError, amounts) = await ComputePayrollAmountsAsync(entry.UserId, entry.ClientId, entry.ProjectId, body.Date, body.WorkHours, body.OutOfHoursHours, ct);
        if (payrollError is not null) return payrollError;

        entry.Date = body.Date;
        entry.WorkHours = body.WorkHours;
        entry.OutOfHoursHours = body.OutOfHoursHours;
        entry.Description = body.Description;
        entry.EntryTypeId = body.EntryTypeId;
        entry.ToPayroll = amounts!.ToPayroll;
        entry.ToCompany = amounts.ToCompany;
        entry.ResolvedCustomerRate = amounts.ResolvedCustomerRate;
        entry.ResolvedHourlyCost = amounts.ResolvedHourlyCost;
        entry.ResolvedOutOfHoursCost = amounts.ResolvedOutOfHoursCost;
        entry.RateCardId = amounts.RateCardId;
        entry.StaffCostId = amounts.StaffCostId;
        entry.Tier = amounts.Tier;
        entry.BillingPeriodChoice = ResolveBillingPeriodChoice(body.BillingPeriodChoice);
        entry.ModifiedUtc = DateTimeOffset.UtcNow;

        entries.Update(entry);
        await auditLog.LogAsync(user, "TimesheetEntry.Updated", "TimesheetEntry", entry.Id,
            $"{entry.Date:yyyy-MM-dd}, {entry.WorkHours + entry.OutOfHoursHours}h on project {entry.ProjectId}", impersonatedUserId, ct);
        await uow.SaveChangesAsync(ct);
        return new OkObjectResult(ToDto(entry));
    }

    [Function("TimesheetEntries_Delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "timesheet-entries/{id:int}")] HttpRequest req, int id, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var entry = await entries.GetByIdAsync(id, ct);
        if (entry is null) return new NotFoundResult();
        var (authorized, impersonatedUserId) = CheckOwnership(entry, user, ParseOnBehalfOfUserId(req));
        if (!authorized) return new NotFoundResult();
        if (entry.ApprovedPayroll || entry.SentToPayroll) return SentToPayrollLockedResult();
        if (entry.InvoiceId is not null) return InvoicedLockedResult();

        entries.Remove(entry);
        await auditLog.LogAsync(user, "TimesheetEntry.Deleted", "TimesheetEntry", entry.Id,
            $"{entry.Date:yyyy-MM-dd}, {entry.WorkHours + entry.OutOfHoursHours}h on project {entry.ProjectId}", impersonatedUserId, ct);
        await uow.SaveChangesAsync(ct);
        return new NoContentResult();
    }

    /// <summary>Mirrors the legacy app's row "duplicate" icon - clones an entry onto a (usually different) date.</summary>
    [Function("TimesheetEntries_Duplicate")]
    public async Task<IActionResult> Duplicate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "timesheet-entries/{id:int}/duplicate")] HttpRequest req, int id, CancellationToken ct)
    {
        var user = currentUser.RequireUser();
        var source = await entries.GetByIdAsync(id, ct);
        if (source is null) return new NotFoundResult();

        var body = await req.ReadFromJsonAsync<DuplicateTimesheetEntryRequest>(ct);
        var (authorized, impersonatedUserId) = CheckOwnership(source, user, body?.OnBehalfOfUserId);
        if (!authorized) return new NotFoundResult();
        if (source.ApprovedPayroll || source.SentToPayroll) return SentToPayrollLockedResult();
        if (source.InvoiceId is not null) return InvoicedLockedResult();

        var targetDate = body?.Date ?? source.Date;

        var (validationError, project) = await ValidateAssignmentAsync(source.UserId, source.ProjectId, targetDate, ct);
        if (validationError is not null) return validationError;

        var valuesError = await ValidateEntryValuesAsync(project!.ClientId, source.UserId, targetDate, source.WorkHours, source.OutOfHoursHours, excludeEntryId: null, ct);
        if (valuesError is not null) return valuesError;

        var (payrollError, amounts) = await ComputePayrollAmountsAsync(source.UserId, project!.ClientId, source.ProjectId, targetDate, source.WorkHours, source.OutOfHoursHours, ct);
        if (payrollError is not null) return payrollError;

        // Deliberately does NOT carry over the source's ApprovedPayroll/SentToPayroll/audit fields - the legacy
        // app clones them verbatim (a copy of an approved entry is itself created pre-approved), which looks
        // like a bug, not intended behavior. A duplicate is always a fresh, unapproved entry here.
        var copy = new TimesheetEntry
        {
            UserId = source.UserId,
            ClientId = project!.ClientId,
            ProjectId = source.ProjectId,
            Date = targetDate,
            WorkHours = source.WorkHours,
            OutOfHoursHours = source.OutOfHoursHours,
            Description = source.Description,
            EntryTypeId = source.EntryTypeId,
            ToPayroll = amounts!.ToPayroll,
            ToCompany = amounts.ToCompany,
            ResolvedCustomerRate = amounts.ResolvedCustomerRate,
            ResolvedHourlyCost = amounts.ResolvedHourlyCost,
            ResolvedOutOfHoursCost = amounts.ResolvedOutOfHoursCost,
            RateCardId = amounts.RateCardId,
            StaffCostId = amounts.StaffCostId,
            Tier = amounts.Tier,
            BillingPeriodChoice = source.BillingPeriodChoice,
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        await entries.AddAsync(copy, ct);
        await uow.SaveChangesAsync(ct);

        // Two-step save - see the matching comment in Create.
        await auditLog.LogAsync(user, "TimesheetEntry.Duplicated", "TimesheetEntry", copy.Id,
            $"from entry {source.Id}, {copy.Date:yyyy-MM-dd}, {copy.WorkHours + copy.OutOfHoursHours}h on project {copy.ProjectId}", impersonatedUserId, ct);
        await uow.SaveChangesAsync(ct);

        var saved = await entries.GetByIdAsync(copy.Id, ct);
        return new CreatedResult($"/api/timesheet-entries/{copy.Id}", ToDto(saved!));
    }

    /// <summary>Enforces "a User can only log time against a Project they're assigned to" - date-aware, checked
    /// at the service layer since assignment validity depends on the assignment's active date range, not just
    /// row existence (see IStaffProjectRepository.IsUserAssignedAsync). Returns the resolved Project alongside
    /// any error so callers don't need a second lookup to stamp ClientId onto the entry.</summary>
    private async Task<(IActionResult? Error, Project? Project)> ValidateAssignmentAsync(int userId, int projectId, DateOnly date, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(projectId, ct);
        if (project is null) return (new NotFoundObjectResult(new { error = "Project not found." }), null);

        if (!await assignments.IsUserAssignedAsync(userId, projectId, date, ct))
        {
            return (new ObjectResult(new { error = "You are not assigned to this project for the given date." })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            }, null);
        }
        return (null, project);
    }

    /// <summary>Enforces the legacy Power App's timesheet data-quality rules (no future dates, no dates before
    /// the client relationship started, positive hours, half-hour granularity) - these were never encoded
    /// anywhere in the new app before, so callers must run this on every Create/Update/Duplicate.</summary>
    private async Task<IActionResult?> ValidateEntryValuesAsync(int clientId, int userId, DateOnly date, decimal workHours, decimal outOfHoursHours, int? excludeEntryId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
        if (date > today)
        {
            return new BadRequestObjectResult(new { error = "The date cannot be in the future." });
        }

        var client = await clients.GetByIdAsync(clientId, ct);
        if (client is not null && date < client.StartDate)
        {
            return new BadRequestObjectResult(new { error = $"The date cannot be before this client's start date ({client.StartDate:yyyy-MM-dd})." });
        }

        if (workHours < 0 || outOfHoursHours < 0)
        {
            return new BadRequestObjectResult(new { error = "Hours cannot be negative." });
        }

        if (workHours + outOfHoursHours <= 0)
        {
            return new BadRequestObjectResult(new { error = "At least one of Work Hours or Out of Hours must be greater than zero." });
        }

        if (workHours + outOfHoursHours > 24)
        {
            return new BadRequestObjectResult(new { error = "Total hours for a single entry cannot exceed 24." });
        }

        var otherHours = await entries.GetTotalHoursForUserDateAsync(userId, date, excludeEntryId, ct);
        var dayTotal = otherHours + workHours + outOfHoursHours;
        if (dayTotal > 24)
        {
            return new BadRequestObjectResult(new { error = $"Total hours for {date:yyyy-MM-dd} would be {dayTotal:0.##}, which exceeds the 24-hour daily limit." });
        }

        if (workHours % 0.5m != 0 || outOfHoursHours % 0.5m != 0)
        {
            return new BadRequestObjectResult(new { error = "Hours must be entered in half-hour increments." });
        }

        return null;
    }

    /// <summary>EntryTypeId is optional (many projects have no EntryTypes configured yet), but when supplied
    /// must belong to this entry's own Project and be active - and, defense-in-depth alongside
    /// EntryTypesFunctions.Create's own check, a Contract EntryType may only be used on a Contract-type
    /// Project (FDD: "Only projects of type Contract accept contract entries").</summary>
    private async Task<IActionResult?> ValidateEntryTypeAsync(int? entryTypeId, int projectId, ProjectType projectType, CancellationToken ct)
    {
        if (entryTypeId is null) return null;

        var entryType = await entryTypes.GetByIdAsync(entryTypeId.Value, ct);
        if (entryType is null || entryType.ProjectId != projectId || !entryType.IsActive)
        {
            return new BadRequestObjectResult(new { error = "Entry type is not valid for this project." });
        }
        if (entryType.IsContractType && projectType != ProjectType.Contract)
        {
            return new BadRequestObjectResult(new { error = "Only a Contract-type project can use a Contract entry type." });
        }
        return null;
    }

    /// <summary>Resolves and stamps everything a save needs to know about the rate/cost that applied - both
    /// the ToPayroll/ToCompany totals (Reports/Invoicing sum the same ResolvedCustomerRate/ResolvedHourlyCost/
    /// ResolvedOutOfHoursCost fields directly rather than re-resolving, so they always agree with what an entry
    /// was actually saved at) and the snapshot fields themselves, resolved as of the entry's own Date - never
    /// "now" - so a later rate/cost change never retroactively affects an entry unless it's itself re-saved.
    /// A RateNotConfiguredException is a real data-entry gap (no Role/override, or no StaffCost) - returned as
    /// a clean 409, never silently defaulted to zero.</summary>
    private async Task<(IActionResult? Error, PayrollAmounts? Amounts)> ComputePayrollAmountsAsync(
        int staffId, int clientId, int projectId, DateOnly date, decimal workHours, decimal outOfHoursHours, CancellationToken ct)
    {
        try
        {
            var resolution = await rateResolver.ResolveAsync(staffId, clientId, projectId, date, ct);
            var toPayroll = (workHours + outOfHoursHours) * resolution.CustomerRate;
            var toCompany = workHours * resolution.HourlyCost + outOfHoursHours * resolution.OutOfHoursCost;
            return (null, new PayrollAmounts(
                toPayroll, toCompany, resolution.CustomerRate, resolution.HourlyCost, resolution.OutOfHoursCost,
                resolution.RateCardId, resolution.StaffCostId, resolution.Tier));
        }
        catch (RateNotConfiguredException ex)
        {
            return (new ConflictObjectResult(new { error = ex.Message }), null);
        }
    }

    private record PayrollAmounts(
        decimal ToPayroll, decimal ToCompany, decimal ResolvedCustomerRate, decimal ResolvedHourlyCost, decimal ResolvedOutOfHoursCost,
        int RateCardId, int StaffCostId, RateCardTier Tier);

    private static TimesheetEntrySummaryDto BuildSummary(IReadOnlyList<TimesheetEntry> result)
    {
        var mostRecentDate = result.Count == 0 ? (DateOnly?)null : result.Max(e => e.Date);
        var mostRecentDayHours = mostRecentDate is null
            ? 0
            : result.Where(e => e.Date == mostRecentDate).Sum(e => e.WorkHours + e.OutOfHoursHours);

        var totalWork = result.Sum(e => e.WorkHours);
        var totalOutOfHours = result.Sum(e => e.OutOfHoursHours);
        return new TimesheetEntrySummaryDto(mostRecentDayHours, totalWork, totalOutOfHours, totalWork + totalOutOfHours);
    }

    /// <summary>Once an entry has been approved for payroll (or sent) it's immutable - matches the legacy app's
    /// Edit/Delete/Copy icons being hidden once approved, enforced here rather than only in the UI. Editing or
    /// deleting an approved-but-not-yet-sent entry would silently invalidate the approval it already has.</summary>
    private static IActionResult SentToPayrollLockedResult() =>
        new ObjectResult(new { error = "This entry has already been approved for payroll and can no longer be changed." })
        {
            StatusCode = StatusCodes.Status409Conflict,
        };

    /// <summary>FDD: "Finalizing an invoice locks the entries it was built from." Set by
    /// InvoicingService.FinalizeInvoiceAsync, never here - this is read-only enforcement.</summary>
    private static IActionResult InvoicedLockedResult() =>
        new ObjectResult(new { error = "This entry has been included on a finalized invoice and can no longer be changed." })
        {
            StatusCode = StatusCodes.Status409Conflict,
        };

    /// <summary>Query-string form of impersonation context, for endpoints (Get/List/Delete) that have no JSON
    /// body to carry OnBehalfOfUserId on.</summary>
    private static int? ParseOnBehalfOfUserId(HttpRequest req) =>
        req.Query.TryGetValue("onBehalfOfUserId", out var v) && int.TryParse(v, out var id) ? id : null;

    /// <summary>List/Get's "whose entries" gate - an Admin may view another user's entries by supplying their
    /// id; anyone else is confined to their own.</summary>
    private static (IActionResult? Error, int EffectiveUserId) ResolveViewTarget(CurrentUserContext user, int? onBehalfOfUserId)
    {
        if (onBehalfOfUserId is { } id && id != user.UserId)
        {
            if (!user.IsAdmin)
            {
                return (new ObjectResult(new { error = "Admin role required to view another user's timesheet." })
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                }, 0);
            }
            return (null, id);
        }
        return (null, user.UserId);
    }

    /// <summary>Get/Update/Delete/Duplicate's ownership gate - the caller owns the entry outright, or is an
    /// Admin actively impersonating its owner (onBehalfOfUserId must equal the entry's own UserId, matching
    /// Create's existing impersonation check). Returns the impersonated user id for AuditLog when the second
    /// branch is what authorized the call, so a plain self-edit never gets tagged as impersonation.</summary>
    private static (bool Authorized, int? ImpersonatedUserId) CheckOwnership(TimesheetEntry entry, CurrentUserContext user, int? onBehalfOfUserId)
    {
        if (entry.UserId == user.UserId) return (true, null);
        if (user.IsAdmin && onBehalfOfUserId == entry.UserId) return (true, entry.UserId);
        return (false, null);
    }

    /// <summary>Null or unrecognized (e.g. omitted by an older client) defaults to Current - the pre-existing
    /// behavior for every entry predating this feature.</summary>
    private static BillingPeriodChoice ResolveBillingPeriodChoice(string? value) =>
        Enum.TryParse<BillingPeriodChoice>(value, out var parsed) ? parsed : BillingPeriodChoice.Current;

    private static TimesheetEntryDto ToDto(TimesheetEntry e) => new(
        e.Id, e.ProjectId, e.Project?.Name ?? "", e.ClientId, e.Client?.Name ?? e.Project?.Client?.Name ?? "",
        e.Date, e.WorkHours, e.OutOfHoursHours, e.Description,
        e.ToPayroll, e.ApprovedPayroll, e.SentToPayroll,
        e.Attachments.Select(a => new AttachmentDto(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedAtUtc)).ToList(),
        e.EntryTypeId, e.EntryType?.Name, e.BillingPeriodChoice.ToString(), e.InvoiceId is not null);
}

public record DuplicateTimesheetEntryRequest(DateOnly? Date, int? OnBehalfOfUserId = null);
