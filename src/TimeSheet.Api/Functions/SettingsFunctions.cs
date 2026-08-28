using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TimeSheet.Api.Auth;
using TimeSheet.Contracts;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Functions;

/// <summary>Firm-wide settings (singleton row, Id = 1) - admin-only. FDD: project consumption notification
/// percentages "are held in settings and can be changed by management" - see BudgetMonitoringService, which
/// reads these instead of a per-project value.</summary>
public class SettingsFunctions(IAppSettingsRepository settings, IUnitOfWork uow, ICurrentUserAccessor currentUser)
{
    [Function("Settings_Get")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "settings")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var s = await settings.GetAsync(ct);
        return new OkObjectResult(ToDto(s));
    }

    [Function("Settings_Update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "settings")] HttpRequest req, CancellationToken ct)
    {
        if (currentUser.RequireAdmin() is { } forbidden) return forbidden;

        var body = await req.ReadFromJsonAsync<UpdateAppSettingsRequest>(ct)
            ?? throw new BadHttpRequestException("Missing request body.");

        if (string.IsNullOrWhiteSpace(body.BaseReportingCurrency) || body.BaseReportingCurrency.Length != 3)
        {
            return new BadRequestObjectResult(new { error = "Base reporting currency must be a 3-letter code." });
        }
        if (body.DefaultInvoiceMonthEndDay is < 1 or > 31)
        {
            return new BadRequestObjectResult(new { error = "Default invoice month-end day must be between 1 and 31." });
        }
        if (body.ProjectBudgetWarningThresholdPercent is <= 0 or > 100 || body.ProjectBudgetAlertThresholdPercent is <= 0 or > 100)
        {
            return new BadRequestObjectResult(new { error = "Notification thresholds must be between 1 and 100." });
        }
        if (body.ProjectBudgetWarningThresholdPercent >= body.ProjectBudgetAlertThresholdPercent)
        {
            return new BadRequestObjectResult(new { error = "The warning threshold must be lower than the alert threshold." });
        }

        var s = await settings.GetAsync(ct);
        s.BaseReportingCurrency = body.BaseReportingCurrency.ToUpperInvariant();
        s.DefaultInvoiceMonthEndDay = body.DefaultInvoiceMonthEndDay;
        s.ProjectBudgetWarningThresholdPercent = body.ProjectBudgetWarningThresholdPercent;
        s.ProjectBudgetAlertThresholdPercent = body.ProjectBudgetAlertThresholdPercent;

        settings.Update(s);
        await uow.SaveChangesAsync(ct);
        return new OkObjectResult(ToDto(s));
    }

    private static AppSettingsDto ToDto(AppSettings s) => new(
        s.BaseReportingCurrency, s.DefaultInvoiceMonthEndDay,
        s.ProjectBudgetWarningThresholdPercent, s.ProjectBudgetAlertThresholdPercent);
}
