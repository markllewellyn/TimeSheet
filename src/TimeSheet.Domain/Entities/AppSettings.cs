namespace TimeSheet.Domain.Entities;

/// <summary>Singleton settings row (single record, Id = 1). Seeds new Clients and covers firm-wide (unscoped) reports.</summary>
public class AppSettings
{
    public int Id { get; set; } = 1;

    /// <summary>Used to normalize firm-wide reports that have no single client/project currency to fall back to.</summary>
    public required string BaseReportingCurrency { get; set; }

    public int DefaultInvoiceMonthEndDay { get; set; } = 31;

    /// <summary>FDD: "Project feedback notifications are raised at 50% and 75% of the time allotted to a
    /// project... The percentages are held in settings and can be changed by management." Firm-wide, not
    /// per-project - see BudgetMonitoringService, which reads these instead of a per-project value.</summary>
    public int ProjectBudgetWarningThresholdPercent { get; set; } = 50;

    /// <summary>The second, more urgent notification point - see ProjectBudgetWarningThresholdPercent.</summary>
    public int ProjectBudgetAlertThresholdPercent { get; set; } = 75;
}
