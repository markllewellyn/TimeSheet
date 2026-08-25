using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class ReportingService(
    IReportingRepository reportingRepository,
    IClientRepository clients,
    IProjectRepository projects,
    IRateResolver rateResolver,
    ICurrencyConversionService currencyConversion,
    IRevenueRecognitionService revenueRecognition) : IReportingService
{
    public async Task<ReportEnvelope<TimeSummary, TimeByUserLine>> GetTimeOnProjectReportAsync(int projectId, ReportDateRange range, CancellationToken ct)
    {
        var rows = await reportingRepository.GetTimeEntryAggregatesAsync(new ReportScope(null, projectId), range.Start, range.End, ct);
        var currency = await ResolveProjectCurrencyAsync(projectId, ct);

        var byUser = rows
            .GroupBy(r => (r.UserId, r.UserName))
            .Select(g => new TimeByUserLine(g.Key.UserId, g.Key.UserName, g.Sum(x => x.WorkHours), g.Sum(x => x.OutOfHoursHours), g.Sum(x => x.WorkHours + x.OutOfHoursHours)))
            .OrderByDescending(l => l.TotalHours)
            .ToList();

        return new ReportEnvelope<TimeSummary, TimeByUserLine>(range, currency, SummarizeTime(rows), byUser);
    }

    public async Task<ReportEnvelope<TimeSummary, TimeByProjectLine>> GetTimeOnClientReportAsync(int clientId, ReportDateRange range, CancellationToken ct)
    {
        var rows = await reportingRepository.GetTimeEntryAggregatesAsync(new ReportScope(clientId, null), range.Start, range.End, ct);
        var client = await clients.GetByIdAsync(clientId, ct);
        var currency = client?.ReportingCurrencyCode ?? "GBP";

        var byProject = rows
            .GroupBy(r => (r.ProjectId, r.ProjectName))
            .Select(g => new TimeByProjectLine(g.Key.ProjectId, g.Key.ProjectName, g.Sum(x => x.WorkHours), g.Sum(x => x.OutOfHoursHours), g.Sum(x => x.WorkHours + x.OutOfHoursHours)))
            .OrderByDescending(l => l.TotalHours)
            .ToList();

        return new ReportEnvelope<TimeSummary, TimeByProjectLine>(range, currency, SummarizeTime(rows), byProject);
    }

    public async Task<ReportEnvelope<CostSummary, CostByUserLine>> GetCostOnProjectReportAsync(int projectId, ReportDateRange range, CancellationToken ct)
    {
        var rows = await reportingRepository.GetTimeEntryAggregatesAsync(new ReportScope(null, projectId), range.Start, range.End, ct);
        var expenseRows = await reportingRepository.GetExpenseAggregatesAsync(new ReportScope(null, projectId), range.Start, range.End, ct);
        var currency = await ResolveProjectCurrencyAsync(projectId, ct);
        var projectCurrency = await ResolveNativeProjectCurrencyAsync(projectId, ct);

        var byUser = new List<CostByUserLine>();
        foreach (var g in rows.GroupBy(r => (r.UserId, r.UserName)))
        {
            var hours = g.Sum(x => x.WorkHours + x.OutOfHoursHours);
            var rate = await rateResolver.GetEffectiveRateAsync(projectId, g.Key.UserId, range.End, ct);
            var laborCost = await currencyConversion.ConvertAsync(new Money(hours * rate.CostRatePerHour, projectCurrency), currency, range.End, ct);
            byUser.Add(new CostByUserLine(g.Key.UserId, g.Key.UserName, laborCost.Amount, 0, laborCost.Amount));
        }

        var expenseCost = await SumConvertedAsync(expenseRows.Select(e => (e.Amount, e.Currency, e.Date)), currency, ct);
        var summary = new CostSummary(byUser.Sum(l => l.LaborCost), expenseCost, byUser.Sum(l => l.LaborCost) + expenseCost);
        return new ReportEnvelope<CostSummary, CostByUserLine>(range, currency, summary, byUser.OrderByDescending(l => l.TotalCost).ToList());
    }

    public async Task<ReportEnvelope<CostSummary, CostByProjectLine>> GetCostOnClientReportAsync(int clientId, ReportDateRange range, CancellationToken ct)
    {
        var rows = await reportingRepository.GetTimeEntryAggregatesAsync(new ReportScope(clientId, null), range.Start, range.End, ct);
        var expenseRows = await reportingRepository.GetExpenseAggregatesAsync(new ReportScope(clientId, null), range.Start, range.End, ct);
        var client = await clients.GetByIdAsync(clientId, ct);
        var currency = client?.ReportingCurrencyCode ?? "GBP";

        var byProject = new List<CostByProjectLine>();
        foreach (var projectGroup in rows.GroupBy(r => (r.ProjectId, r.ProjectName)))
        {
            var projectCurrency = await ResolveNativeProjectCurrencyAsync(projectGroup.Key.ProjectId, ct);
            decimal laborCostNative = 0;
            foreach (var userGroup in projectGroup.GroupBy(r => r.UserId))
            {
                var hours = userGroup.Sum(x => x.WorkHours + x.OutOfHoursHours);
                var rate = await rateResolver.GetEffectiveRateAsync(projectGroup.Key.ProjectId, userGroup.Key, range.End, ct);
                laborCostNative += hours * rate.CostRatePerHour;
            }
            var laborCost = await currencyConversion.ConvertAsync(new Money(laborCostNative, projectCurrency), currency, range.End, ct);
            var expenseCost = await SumConvertedAsync(
                expenseRows.Where(e => e.ProjectId == projectGroup.Key.ProjectId).Select(e => (e.Amount, e.Currency, e.Date)), currency, ct);

            byProject.Add(new CostByProjectLine(projectGroup.Key.ProjectId, projectGroup.Key.ProjectName, laborCost.Amount, expenseCost, laborCost.Amount + expenseCost));
        }

        var summary = new CostSummary(byProject.Sum(l => l.LaborCost), byProject.Sum(l => l.ExpenseCost), byProject.Sum(l => l.TotalCost));
        return new ReportEnvelope<CostSummary, CostByProjectLine>(range, currency, summary, byProject.OrderByDescending(l => l.TotalCost).ToList());
    }

    public async Task<ReportEnvelope<ProfitSummary, ProfitByUserLine>> GetProfitOnProjectReportAsync(int projectId, ReportDateRange range, CancellationToken ct)
    {
        var rows = await reportingRepository.GetTimeEntryAggregatesAsync(new ReportScope(null, projectId), range.Start, range.End, ct);
        var expenseRows = await reportingRepository.GetExpenseAggregatesAsync(new ReportScope(null, projectId), range.Start, range.End, ct);
        var currency = await ResolveProjectCurrencyAsync(projectId, ct);
        var projectCurrency = await ResolveNativeProjectCurrencyAsync(projectId, ct);
        var project = await projects.GetByIdAsync(projectId, ct);

        var byUser = new List<ProfitByUserLine>();
        foreach (var g in rows.GroupBy(r => (r.UserId, r.UserName)))
        {
            var hours = g.Sum(x => x.WorkHours + x.OutOfHoursHours);
            var rate = await rateResolver.GetEffectiveRateAsync(projectId, g.Key.UserId, range.End, ct);
            var cost = await currencyConversion.ConvertAsync(new Money(hours * rate.CostRatePerHour, projectCurrency), currency, range.End, ct);

            decimal billedAmount;
            if (project?.PaymentModel == PaymentModel.FixedProjectCost)
            {
                billedAmount = 0; // Fixed-fee revenue is recognized at the project level, not per-user - see the project-level line below.
            }
            else
            {
                var billed = await currencyConversion.ConvertAsync(new Money(hours * (rate.BillingRatePerHour ?? 0), projectCurrency), currency, range.End, ct);
                billedAmount = billed.Amount;
            }

            byUser.Add(new ProfitByUserLine(g.Key.UserId, g.Key.UserName, billedAmount, cost.Amount, billedAmount - cost.Amount));
        }

        var expenseCost = await SumConvertedAsync(expenseRows.Select(e => (e.Amount, e.Currency, e.Date)), currency, ct);
        var totalHours = rows.Sum(r => r.WorkHours + r.OutOfHoursHours);
        var totalCost = byUser.Sum(l => l.Cost) + expenseCost;

        var totalBilled = project?.PaymentModel == PaymentModel.FixedProjectCost
            ? await revenueRecognition.GetRecognizedRevenueAsync(projectId, totalHours, currency, range.End, ct)
            : byUser.Sum(l => l.Billed);

        var profit = totalBilled - totalCost;
        var margin = totalBilled == 0 ? 0 : Math.Round(profit / totalBilled * 100, 2);
        return new ReportEnvelope<ProfitSummary, ProfitByUserLine>(range, currency, new ProfitSummary(totalBilled, totalCost, profit, margin), byUser.OrderByDescending(l => l.Profit).ToList());
    }

    public async Task<ReportEnvelope<ProfitSummary, ProfitByProjectLine>> GetProfitOnClientReportAsync(int clientId, ReportDateRange range, CancellationToken ct)
    {
        var rows = await reportingRepository.GetTimeEntryAggregatesAsync(new ReportScope(clientId, null), range.Start, range.End, ct);
        var expenseRows = await reportingRepository.GetExpenseAggregatesAsync(new ReportScope(clientId, null), range.Start, range.End, ct);
        var client = await clients.GetByIdAsync(clientId, ct);
        var currency = client?.ReportingCurrencyCode ?? "GBP";

        var byProject = new List<ProfitByProjectLine>();
        foreach (var projectGroup in rows.GroupBy(r => (r.ProjectId, r.ProjectName)))
        {
            var project = await projects.GetByIdAsync(projectGroup.Key.ProjectId, ct);
            var projectCurrency = await ResolveNativeProjectCurrencyAsync(projectGroup.Key.ProjectId, ct);

            decimal costNative = 0;
            decimal billedNative = 0;
            var projectHours = projectGroup.Sum(x => x.WorkHours + x.OutOfHoursHours);

            foreach (var userGroup in projectGroup.GroupBy(r => r.UserId))
            {
                var hours = userGroup.Sum(x => x.WorkHours + x.OutOfHoursHours);
                var rate = await rateResolver.GetEffectiveRateAsync(projectGroup.Key.ProjectId, userGroup.Key, range.End, ct);
                costNative += hours * rate.CostRatePerHour;
                if (project?.PaymentModel != PaymentModel.FixedProjectCost) billedNative += hours * (rate.BillingRatePerHour ?? 0);
            }

            var cost = await currencyConversion.ConvertAsync(new Money(costNative, projectCurrency), currency, range.End, ct);
            var expenseCost = await SumConvertedAsync(
                expenseRows.Where(e => e.ProjectId == projectGroup.Key.ProjectId).Select(e => (e.Amount, e.Currency, e.Date)), currency, ct);

            var billed = project?.PaymentModel == PaymentModel.FixedProjectCost
                ? await revenueRecognition.GetRecognizedRevenueAsync(projectGroup.Key.ProjectId, projectHours, currency, range.End, ct)
                : (await currencyConversion.ConvertAsync(new Money(billedNative, projectCurrency), currency, range.End, ct)).Amount;

            var totalCost = cost.Amount + expenseCost;
            var profit = billed - totalCost;
            var margin = billed == 0 ? 0 : Math.Round(profit / billed * 100, 2);
            byProject.Add(new ProfitByProjectLine(projectGroup.Key.ProjectId, projectGroup.Key.ProjectName, billed, totalCost, profit, margin));
        }

        var summaryBilled = byProject.Sum(l => l.Billed);
        var summaryCost = byProject.Sum(l => l.Cost);
        var summaryProfit = summaryBilled - summaryCost;
        var summaryMargin = summaryBilled == 0 ? 0 : Math.Round(summaryProfit / summaryBilled * 100, 2);

        return new ReportEnvelope<ProfitSummary, ProfitByProjectLine>(
            range, currency, new ProfitSummary(summaryBilled, summaryCost, summaryProfit, summaryMargin),
            byProject.OrderByDescending(l => l.Profit).ToList());
    }

    private static TimeSummary SummarizeTime(IReadOnlyList<TimeEntryAggregateRow> rows) => new(
        rows.Sum(r => r.WorkHours), rows.Sum(r => r.OutOfHoursHours), rows.Sum(r => r.WorkHours + r.OutOfHoursHours));

    private async Task<decimal> SumConvertedAsync(IEnumerable<(decimal Amount, string Currency, DateOnly Date)> amounts, string targetCurrency, CancellationToken ct)
    {
        decimal total = 0;
        foreach (var (amount, sourceCurrency, date) in amounts)
        {
            var converted = await currencyConversion.ConvertAsync(new Money(amount, sourceCurrency), targetCurrency, date, ct);
            total += converted.Amount;
        }
        return total;
    }

    /// <summary>The reporting currency a "Time/Cost/Profit on Project" report is presented in - the project's
    /// client's reporting currency (project-level CurrencyOverride only affects the project's own native rate
    /// currency, not the currency the report itself is shown in).</summary>
    private async Task<string> ResolveProjectCurrencyAsync(int projectId, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(projectId, ct);
        if (project is null) return "GBP";
        var client = await clients.GetByIdAsync(project.ClientId, ct);
        return client?.ReportingCurrencyCode ?? "GBP";
    }

    private async Task<string> ResolveNativeProjectCurrencyAsync(int projectId, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(projectId, ct);
        if (project?.CurrencyOverride is not null) return project.CurrencyOverride;
        return await ResolveProjectCurrencyAsync(projectId, ct);
    }
}
