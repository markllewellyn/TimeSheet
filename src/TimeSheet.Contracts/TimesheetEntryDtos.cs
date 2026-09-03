namespace TimeSheet.Contracts;

public record TimesheetEntryDto(
    int Id, int ProjectId, string ProjectName, int ClientId, string ClientName,
    DateOnly Date, decimal WorkHours, decimal OutOfHoursHours, string? Description,
    decimal ToPayroll, bool ApprovedPayroll, bool SentToPayroll,
    IReadOnlyList<AttachmentDto> Attachments, int? EntryTypeId, string? EntryTypeName, string BillingPeriodChoice,
    bool Invoiced);

/// <summary>AdminSendToPayroll mirrors the legacy app's admin-only "Sent to Payroll" checkbox on Add - if
/// true, the created entry is simultaneously approved and sent in one step. OnBehalfOfUserId lets an Admin log
/// time for another staff member (impersonation) - the entry is stamped UserId=that person,
/// CreatedByUserId=the admin, so the two are never conflated. Both are ignored server-side unless the caller is
/// an Admin (see TimesheetEntriesFunctions.Create). EntryTypeId is optional - many projects have no EntryTypes
/// configured yet - but when supplied must belong to this ProjectId and be active (see ValidateEntryTypeAsync).
/// BillingPeriodChoice is "Current" or "Next" (FDD) - null/unrecognized defaults to "Current" server-side (see
/// TimesheetEntriesFunctions.ResolveBillingPeriodChoice).</summary>
public record CreateTimesheetEntryRequest(int ProjectId, DateOnly Date, decimal WorkHours, decimal OutOfHoursHours, string? Description, bool? AdminSendToPayroll = null, int? OnBehalfOfUserId = null, int? EntryTypeId = null, string? BillingPeriodChoice = null);

/// <summary>OnBehalfOfUserId lets an Admin edit an entry while impersonating its owner - must equal the
/// entry's own UserId (see TimesheetEntriesFunctions.Update), ignored unless the caller is an Admin.</summary>
public record UpdateTimesheetEntryRequest(DateOnly Date, decimal WorkHours, decimal OutOfHoursHours, string? Description, int? OnBehalfOfUserId = null, int? EntryTypeId = null, string? BillingPeriodChoice = null);

public record TimesheetEntrySummaryDto(
    decimal MostRecentDayHours, decimal TotalWorkHours, decimal TotalOutOfHoursHours, decimal TotalHours);

public record AttachmentDto(int Id, string FileName, string ContentType, long SizeBytes, DateTimeOffset UploadedAtUtc);
