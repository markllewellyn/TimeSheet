namespace TimeSheet.Contracts;

/// <summary>One row per (Client, Project) the caller has ever logged time against - mirrors the legacy app's
/// "Staff Overview" screen. SentToPayroll is true only once every entry in the group has been sent (a mixed
/// group of sent/unsent entries reads as "not fully processed yet").</summary>
public record MyOverviewLineDto(
    int ClientId, string ClientName, int ProjectId, string ProjectName,
    decimal WorkHours, decimal OutOfHoursHours, decimal ToPayroll, bool SentToPayroll);
