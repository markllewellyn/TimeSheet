using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class ProjectBreakdownService(
    ITimesheetEntryRepository entries, IClientRepository clients, IRevenueRecognitionService revenueRecognition) : IProjectBreakdownService
{
    public async Task<ProjectBreakdown> GetBreakdownAsync(Project project, CancellationToken ct)
    {
        var rows = await entries.GetAllCountedForProjectAsync(project.Id, ct);
        var canInvoice = project.CanInvoice == true;
        var isFixedFee = project.PaymentModel == PaymentModel.FixedProjectCost;

        decimal? recognizedRevenue = null;
        if (isFixedFee && canInvoice)
        {
            var totalHours = rows.Sum(e => e.WorkHours + e.OutOfHoursHours);
            var nativeCurrency = await ResolveNativeCurrencyAsync(project, ct);
            recognizedRevenue = await revenueRecognition.GetRecognizedRevenueAsync(
                project.Id, hoursBeforePeriod: 0, totalHours, nativeCurrency, DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date), ct);
        }

        var staffGroups = rows.GroupBy(e => (e.UserId, UserName: e.User?.DisplayName ?? "Unknown")).ToList();
        // Fixed Fee: each person gets their hours-share of the project's recognized revenue (same split as
        // ReportingService's per-row Profit reports), so the column sums to RecognizedRevenueToDate exactly - not a
        // misleading hours*rate figure, and not 0 (which read as a loss per person).
        var fixedFeeShares = recognizedRevenue is { } recognized
            ? RevenueAllocation.ByHours(recognized, staffGroups.Select(g => g.Sum(e => e.WorkHours + e.OutOfHoursHours)).ToList())
            : null;

        var byStaff = staffGroups
            .Select((g, i) =>
            {
                var hours = g.Sum(e => e.WorkHours + e.OutOfHoursHours);
                var cost = g.Sum(e => e.WorkHours * (e.ResolvedHourlyCost ?? 0) + e.OutOfHoursHours * (e.ResolvedOutOfHoursCost ?? 0));
                var revenue = fixedFeeShares?[i]
                    ?? (canInvoice && !isFixedFee ? g.Sum(e => (e.WorkHours + e.OutOfHoursHours) * (e.ResolvedCustomerRate ?? 0)) : 0m);
                return new StaffBreakdownLine(g.Key.UserId, g.Key.UserName, hours, cost, revenue, revenue - cost);
            })
            .OrderByDescending(l => l.Hours)
            .ToList();

        var byEntryType = rows
            .GroupBy(e => e.EntryTypeId)
            .Select(g => new EntryTypeBreakdownLine(
                g.Key, g.Key is null ? "Unspecified" : (g.First().EntryType?.Name ?? "Unspecified"), g.Sum(e => e.WorkHours + e.OutOfHoursHours)))
            .OrderByDescending(l => l.Hours)
            .ToList();

        var byMonth = rows
            .GroupBy(e => (e.Date.Year, e.Date.Month))
            .Select(g => new MonthBreakdownLine(g.Key.Year, g.Key.Month, g.Sum(e => e.WorkHours + e.OutOfHoursHours)))
            .OrderBy(l => l.Year).ThenBy(l => l.Month)
            .ToList();

        return new ProjectBreakdown(project.Id, byStaff, byEntryType, byMonth, recognizedRevenue);
    }

    // Mirrors ReportingService.ResolveNativeProjectCurrencyAsync exactly - Project.Client (loaded via
    // IProjectRepository.GetByIdAsync) does NOT include Client.Currency, so Client.ReportingCurrencyCode would
    // silently fall back to "GBP" without a fresh, properly-hydrated fetch via IClientRepository.
    private async Task<string> ResolveNativeCurrencyAsync(Project project, CancellationToken ct)
    {
        if (project.CurrencyOverride is not null) return project.CurrencyOverride;
        var client = await clients.GetByIdAsync(project.ClientId, ct);
        return client?.ReportingCurrencyCode ?? "GBP";
    }
}
