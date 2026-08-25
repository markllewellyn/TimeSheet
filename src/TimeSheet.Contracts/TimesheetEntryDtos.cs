namespace TimeSheet.Contracts;

public record TimesheetEntryDto(
    int Id, int ProjectId, string ProjectName, int ClientId, string ClientName,
    DateOnly Date, decimal WorkHours, decimal OutOfHoursHours, string? Description, string Status,
    IReadOnlyList<AttachmentDto> Attachments);

public record CreateTimesheetEntryRequest(int ProjectId, DateOnly Date, decimal WorkHours, decimal OutOfHoursHours, string? Description);

public record UpdateTimesheetEntryRequest(DateOnly Date, decimal WorkHours, decimal OutOfHoursHours, string? Description);

public record TimesheetEntrySummaryDto(
    decimal MostRecentDayHours, decimal TotalWorkHours, decimal TotalOutOfHoursHours, decimal TotalHours);

public record AttachmentDto(int Id, string FileName, string ContentType, long SizeBytes, DateTimeOffset UploadedAtUtc);
