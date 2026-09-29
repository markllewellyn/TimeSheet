namespace TimeSheet.Contracts;

public record ProjectDto(
    int Id, int ClientId, string ClientName, string Name, string Code, string? Description,
    string PaymentModel, string ProjectType, bool? CanInvoice, bool IsCostExempt, string? CurrencyOverride, DateOnly StartDate, DateOnly? EndDate,
    decimal? BudgetHours, decimal? FixedFeeAmount, bool IsActive,
    int? ProjectManagerUserId, string? ProjectManagerName, int AssignedStaffCount,
    // Only populated by the list endpoints an Admin or a project's own PM can call (Projects_ListAll,
    // Projects_ListByClient, Projects_ListManagedByMe) - null on every other ProjectDto response (Get/Create/
    // Update/ListAssignedToMe), which a regular assigned staff member can also reach and must not see project
    // financials through. See ProjectsFunctions.ToDto's optional status parameter.
    decimal? ActualHours = null, decimal? HoursUsedPercent = null, decimal? ActualCost = null, decimal? CostUsedPercent = null,
    // The owning client's own IsActive - lets the Add Entry/Add Expense pickers hide an inactive client's projects
    // (new entries against one are refused server-side, see InactiveClientGuard). Null when Client wasn't loaded.
    bool? ClientIsActive = null);

public record CreateProjectRequest(
    int ClientId, string Name, string Code, string? Description,
    string PaymentModel, string ProjectType, bool? CanInvoice, bool IsCostExempt, string? CurrencyOverride, DateOnly StartDate, DateOnly? EndDate,
    decimal? BudgetHours, decimal? FixedFeeAmount, int? ProjectManagerUserId);

public record UpdateProjectRequest(
    string Name, string? Description, string ProjectType, bool? CanInvoice, bool IsCostExempt, string? CurrencyOverride, DateOnly? EndDate,
    decimal? BudgetHours, decimal? FixedFeeAmount, bool IsActive, int? ProjectManagerUserId);

/// <summary>FDD: "Attachments and documents can be held against a project." UploadedByName is included (unlike
/// the entry-scoped AttachmentDto) since a project's documents are seen by potentially several people - Admin
/// and PM alike - not just the one owner an entry has.</summary>
public record ProjectAttachmentDto(int Id, string FileName, string ContentType, long SizeBytes, DateTimeOffset UploadedAtUtc, string UploadedByName);
