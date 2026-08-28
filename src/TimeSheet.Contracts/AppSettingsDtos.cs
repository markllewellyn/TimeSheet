namespace TimeSheet.Contracts;

public record AppSettingsDto(
    string BaseReportingCurrency, int DefaultInvoiceMonthEndDay,
    int ProjectBudgetWarningThresholdPercent, int ProjectBudgetAlertThresholdPercent);

public record UpdateAppSettingsRequest(
    string BaseReportingCurrency, int DefaultInvoiceMonthEndDay,
    int ProjectBudgetWarningThresholdPercent, int ProjectBudgetAlertThresholdPercent);
