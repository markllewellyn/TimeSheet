using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class ReportingService(
    IReportingRepository reportingRepository,
    IClientRepository clients,
    IProjectRepository projects,
    ICurrencyConversionService currencyConversion,
    IRevenueRecognitionService revenueRecognition,
    IProjectStatusService projectStatus) : IReportingService
{
    public async Task<ReportEnvelope<TimeSummary, TimeByUserLine>> GetTimeOnProjectReportAsync(int projectId, ReportDateRange range, CancellationToken ct)
    {
        var rows = await reportingRepository.GetTimeEntryAggregatesAsync(new ReportScope(null, projectId), range.Start, range.End, ct);
        var currency = await ResolveProjectCurrencyAsync(projectId, ct);

        var byUser = rows
            .GroupBy(r => (r.UserId, r.UserName))
            .Select(g => new TimeByUserLine(g.Key.UserId, g.Key.UserName, g.Sum(x => x.WorkHours), g.Sum(x => x.OutOfHoursHours), g.Sum(x => x.WorkHours + x.OutOfHoursHours), g.Sum(x => x.EntryCount)))
            .OrderByDescending(l => l.TotalHours)
            .ToList();

        var status = await GetProjectStatusAsync(projectId, ct);
        return new ReportEnvelope<TimeSummary, TimeByUserLine>(range, currency, SummarizeTime(rows, status), byUser);
    }

    public async Task<ReportEnvelope<TimeSummary, TimeByProjectLine>> GetTimeOnClientReportAsync(int clientId, ReportDateRange range, CancellationToken ct)
    {
        var rows = await reportingRepository.GetTimeEntryAggregatesAsync(new ReportScope(clientId, null), range.Start, range.End, ct);
        var client = await clients.GetByIdAsync(clientId, ct);
        var currency = client?.ReportingCurrencyCode ?? "GBP";

        // Bulk-fetched once for the whole client, not per project group below - same batching precedent as
        // IProjectStatusService's own GetStatusesAsync call sites (Projects_ListAll etc.), avoiding an N+1 over
        // however many projects this client has.
        var clientProjects = await projects.GetByClientIdAsync(clientId, includeInactive: true, ct);
        var statuses = await projectStatus.GetStatusesAsync(clientProjects, ct);

        var byProject = rows
            .GroupBy(r => (r.ProjectId, r.ProjectName))
            .Select(g =>
            {
                var status = statuses.GetValueOrDefault(g.Key.ProjectId);
                return new TimeByProjectLine(
                    g.Key.ProjectId, g.Key.ProjectName, g.Sum(x => x.WorkHours), g.Sum(x => x.OutOfHoursHours),
                    g.Sum(x => x.WorkHours + x.OutOfHoursHours), g.Sum(x => x.EntryCount),
                    status?.BudgetHours, HoursRemaining(status?.BudgetHours, status?.ActualHours));
            })
            .OrderByDescending(l => l.TotalHours)
            .ToList();

        // No budgetHours passed here deliberately - see TimeByProjectLine's own doc comment on why a client-wide
        // "hours remaining" isn't published as a single aggregate.
        return new ReportEnvelope<TimeSummary, TimeByProjectLine>(range, currency, SummarizeTime(rows), byProject);
    }

    /// <summary>FDD's "role... basis" ask - see IReportingService's doc comment for why this exists only for
    /// "on Project" (not "on Client") and only reflects each staff member's CURRENT role.</summary>
    public async Task<ReportEnvelope<TimeSummary, TimeByRoleLine>> GetTimeOnProjectByRoleReportAsync(int projectId, ReportDateRange range, CancellationToken ct)
    {
        var rows = await reportingRepository.GetTimeEntryAggregatesAsync(new ReportScope(null, projectId), range.Start, range.End, ct);
        var currency = await ResolveProjectCurrencyAsync(projectId, ct);

        var byRole = rows
            .GroupBy(r => (r.RoleId, r.RoleName))
            .Select(g => new TimeByRoleLine(g.Key.RoleId, g.Key.RoleName, g.Sum(x => x.WorkHours), g.Sum(x => x.OutOfHoursHours), g.Sum(x => x.WorkHours + x.OutOfHoursHours), g.Sum(x => x.EntryCount)))
            .OrderByDescending(l => l.TotalHours)
            .ToList();

        var status = await GetProjectStatusAsync(projectId, ct);
        return new ReportEnvelope<TimeSummary, TimeByRoleLine>(range, currency, SummarizeTime(rows, status), byRole);
    }

    /// <summary>FDD's "team... basis" ask - see IReportingService's doc comment. Mirrors
    /// GetTimeOnProjectByRoleReportAsync exactly, grouping by Team instead of Role.</summary>
    public async Task<ReportEnvelope<TimeSummary, TimeByTeamLine>> GetTimeOnProjectByTeamReportAsync(int projectId, ReportDateRange range, CancellationToken ct)
    {
        var rows = await reportingRepository.GetTimeEntryAggregatesAsync(new ReportScope(null, projectId), range.Start, range.End, ct);
        var currency = await ResolveProjectCurrencyAsync(projectId, ct);

        var byTeam = rows
            .GroupBy(r => (r.TeamId, r.TeamName))
            .Select(g => new TimeByTeamLine(g.Key.TeamId, g.Key.TeamName, g.Sum(x => x.WorkHours), g.Sum(x => x.OutOfHoursHours), g.Sum(x => x.WorkHours + x.OutOfHoursHours), g.Sum(x => x.EntryCount)))
            .OrderByDescending(l => l.TotalHours)
            .ToList();

        var status = await GetProjectStatusAsync(projectId, ct);
        return new ReportEnvelope<TimeSummary, TimeByTeamLine>(range, currency, SummarizeTime(rows, status), byTeam);
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
            var laborCost = await currencyConversion.ConvertAsync(new Money(g.Sum(x => x.CostAmountNative), projectCurrency), currency, range.End, ct);
            byUser.Add(new CostByUserLine(g.Key.UserId, g.Key.UserName, laborCost.Amount, 0, laborCost.Amount, g.Sum(x => x.EntryCount)));
        }

        var expenseCost = await SumConvertedAsync(expenseRows.Select(e => (e.Amount, e.Currency, e.Date)), currency, ct);
        var summary = new CostSummary(byUser.Sum(l => l.LaborCost), expenseCost, byUser.Sum(l => l.LaborCost) + expenseCost, rows.Sum(r => r.EntryCount));
        return new ReportEnvelope<CostSummary, CostByUserLine>(range, currency, summary, byUser.OrderByDescending(l => l.TotalCost).ToList());
    }

    /// <summary>FDD's "role... basis" ask - see GetTimeOnProjectByRoleReportAsync/IReportingService's doc
    /// comment. ExpenseCost is always 0 per role-line - see CostByRoleLine.</summary>
    public async Task<ReportEnvelope<CostSummary, CostByRoleLine>> GetCostOnProjectByRoleReportAsync(int projectId, ReportDateRange range, CancellationToken ct)
    {
        var rows = await reportingRepository.GetTimeEntryAggregatesAsync(new ReportScope(null, projectId), range.Start, range.End, ct);
        var expenseRows = await reportingRepository.GetExpenseAggregatesAsync(new ReportScope(null, projectId), range.Start, range.End, ct);
        var currency = await ResolveProjectCurrencyAsync(projectId, ct);
        var projectCurrency = await ResolveNativeProjectCurrencyAsync(projectId, ct);

        var byRole = new List<CostByRoleLine>();
        foreach (var g in rows.GroupBy(r => (r.RoleId, r.RoleName)))
        {
            var laborCost = await currencyConversion.ConvertAsync(new Money(g.Sum(x => x.CostAmountNative), projectCurrency), currency, range.End, ct);
            byRole.Add(new CostByRoleLine(g.Key.RoleId, g.Key.RoleName, laborCost.Amount, 0, laborCost.Amount, g.Sum(x => x.EntryCount)));
        }

        var expenseCost = await SumConvertedAsync(expenseRows.Select(e => (e.Amount, e.Currency, e.Date)), currency, ct);
        var summary = new CostSummary(byRole.Sum(l => l.LaborCost), expenseCost, byRole.Sum(l => l.LaborCost) + expenseCost, rows.Sum(r => r.EntryCount));
        return new ReportEnvelope<CostSummary, CostByRoleLine>(range, currency, summary, byRole.OrderByDescending(l => l.TotalCost).ToList());
    }

    /// <summary>FDD's "team... basis" ask - see GetTimeOnProjectByTeamReportAsync/IReportingService's doc
    /// comment. ExpenseCost is always 0 per team-line - see CostByTeamLine.</summary>
    public async Task<ReportEnvelope<CostSummary, CostByTeamLine>> GetCostOnProjectByTeamReportAsync(int projectId, ReportDateRange range, CancellationToken ct)
    {
        var rows = await reportingRepository.GetTimeEntryAggregatesAsync(new ReportScope(null, projectId), range.Start, range.End, ct);
        var expenseRows = await reportingRepository.GetExpenseAggregatesAsync(new ReportScope(null, projectId), range.Start, range.End, ct);
        var currency = await ResolveProjectCurrencyAsync(projectId, ct);
        var projectCurrency = await ResolveNativeProjectCurrencyAsync(projectId, ct);

        var byTeam = new List<CostByTeamLine>();
        foreach (var g in rows.GroupBy(r => (r.TeamId, r.TeamName)))
        {
            var laborCost = await currencyConversion.ConvertAsync(new Money(g.Sum(x => x.CostAmountNative), projectCurrency), currency, range.End, ct);
            byTeam.Add(new CostByTeamLine(g.Key.TeamId, g.Key.TeamName, laborCost.Amount, 0, laborCost.Amount, g.Sum(x => x.EntryCount)));
        }

        var expenseCost = await SumConvertedAsync(expenseRows.Select(e => (e.Amount, e.Currency, e.Date)), currency, ct);
        var summary = new CostSummary(byTeam.Sum(l => l.LaborCost), expenseCost, byTeam.Sum(l => l.LaborCost) + expenseCost, rows.Sum(r => r.EntryCount));
        return new ReportEnvelope<CostSummary, CostByTeamLine>(range, currency, summary, byTeam.OrderByDescending(l => l.TotalCost).ToList());
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
            var laborCost = await currencyConversion.ConvertAsync(new Money(projectGroup.Sum(x => x.CostAmountNative), projectCurrency), currency, range.End, ct);
            var expenseCost = await SumConvertedAsync(
                expenseRows.Where(e => e.ProjectId == projectGroup.Key.ProjectId).Select(e => (e.Amount, e.Currency, e.Date)), currency, ct);

            byProject.Add(new CostByProjectLine(projectGroup.Key.ProjectId, projectGroup.Key.ProjectName, laborCost.Amount, expenseCost, laborCost.Amount + expenseCost, projectGroup.Sum(x => x.EntryCount)));
        }

        var summary = new CostSummary(byProject.Sum(l => l.LaborCost), byProject.Sum(l => l.ExpenseCost), byProject.Sum(l => l.TotalCost), rows.Sum(r => r.EntryCount));
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
            var cost = await currencyConversion.ConvertAsync(new Money(g.Sum(x => x.CostAmountNative), projectCurrency), currency, range.End, ct);

            decimal billedAmount;
            if (project?.PaymentModel == PaymentModel.FixedProjectCost)
            {
                billedAmount = 0; // Fixed-fee revenue is recognized at the project level, not per-user - see the project-level line below.
            }
            else
            {
                var billed = await currencyConversion.ConvertAsync(new Money(g.Sum(x => x.BilledAmountNative), projectCurrency), currency, range.End, ct);
                billedAmount = billed.Amount;
            }

            byUser.Add(new ProfitByUserLine(g.Key.UserId, g.Key.UserName, billedAmount, cost.Amount, billedAmount - cost.Amount, g.Sum(x => x.EntryCount)));
        }

        var expenseCost = await SumConvertedAsync(expenseRows.Select(e => (e.Amount, e.Currency, e.Date)), currency, ct);
        var totalHours = rows.Sum(r => r.WorkHours + r.OutOfHoursHours);
        var totalCost = byUser.Sum(l => l.Cost) + expenseCost;

        var totalBilled = project?.PaymentModel == PaymentModel.FixedProjectCost
            ? await revenueRecognition.GetRecognizedRevenueAsync(projectId, totalHours, currency, range.End, ct)
            : byUser.Sum(l => l.Billed);

        var profit = totalBilled - totalCost;
        var margin = totalBilled == 0 ? 0 : Math.Round(profit / totalBilled * 100, 2);
        return new ReportEnvelope<ProfitSummary, ProfitByUserLine>(
            range, currency, new ProfitSummary(totalBilled, totalCost, profit, margin, rows.Sum(r => r.EntryCount)), byUser.OrderByDescending(l => l.Profit).ToList());
    }

    /// <summary>FDD's "role... basis" ask - see GetTimeOnProjectByRoleReportAsync/IReportingService's doc
    /// comment. Fixed Fee revenue is recognized at the whole-project level (billedAmount 0 per role-line, same
    /// as ProfitByUserLine's existing precedent), then added once into the summary's totalBilled below - never
    /// double counted or silently dropped.</summary>
    public async Task<ReportEnvelope<ProfitSummary, ProfitByRoleLine>> GetProfitOnProjectByRoleReportAsync(int projectId, ReportDateRange range, CancellationToken ct)
    {
        var rows = await reportingRepository.GetTimeEntryAggregatesAsync(new ReportScope(null, projectId), range.Start, range.End, ct);
        var expenseRows = await reportingRepository.GetExpenseAggregatesAsync(new ReportScope(null, projectId), range.Start, range.End, ct);
        var currency = await ResolveProjectCurrencyAsync(projectId, ct);
        var projectCurrency = await ResolveNativeProjectCurrencyAsync(projectId, ct);
        var project = await projects.GetByIdAsync(projectId, ct);

        var byRole = new List<ProfitByRoleLine>();
        foreach (var g in rows.GroupBy(r => (r.RoleId, r.RoleName)))
        {
            var cost = await currencyConversion.ConvertAsync(new Money(g.Sum(x => x.CostAmountNative), projectCurrency), currency, range.End, ct);

            decimal billedAmount;
            if (project?.PaymentModel == PaymentModel.FixedProjectCost)
            {
                billedAmount = 0;
            }
            else
            {
                var billed = await currencyConversion.ConvertAsync(new Money(g.Sum(x => x.BilledAmountNative), projectCurrency), currency, range.End, ct);
                billedAmount = billed.Amount;
            }

            byRole.Add(new ProfitByRoleLine(g.Key.RoleId, g.Key.RoleName, billedAmount, cost.Amount, billedAmount - cost.Amount, g.Sum(x => x.EntryCount)));
        }

        var expenseCost = await SumConvertedAsync(expenseRows.Select(e => (e.Amount, e.Currency, e.Date)), currency, ct);
        var totalHours = rows.Sum(r => r.WorkHours + r.OutOfHoursHours);
        var totalCost = byRole.Sum(l => l.Cost) + expenseCost;

        var totalBilled = project?.PaymentModel == PaymentModel.FixedProjectCost
            ? await revenueRecognition.GetRecognizedRevenueAsync(projectId, totalHours, currency, range.End, ct)
            : byRole.Sum(l => l.Billed);

        var profit = totalBilled - totalCost;
        var margin = totalBilled == 0 ? 0 : Math.Round(profit / totalBilled * 100, 2);
        return new ReportEnvelope<ProfitSummary, ProfitByRoleLine>(
            range, currency, new ProfitSummary(totalBilled, totalCost, profit, margin, rows.Sum(r => r.EntryCount)), byRole.OrderByDescending(l => l.Profit).ToList());
    }

    /// <summary>FDD's "team... basis" ask - see GetTimeOnProjectByTeamReportAsync/IReportingService's doc
    /// comment. Fixed Fee revenue is recognized at the whole-project level (billedAmount 0 per team-line, same
    /// as ProfitByRoleLine's existing precedent), then added once into the summary's totalBilled below - never
    /// double counted or silently dropped.</summary>
    public async Task<ReportEnvelope<ProfitSummary, ProfitByTeamLine>> GetProfitOnProjectByTeamReportAsync(int projectId, ReportDateRange range, CancellationToken ct)
    {
        var rows = await reportingRepository.GetTimeEntryAggregatesAsync(new ReportScope(null, projectId), range.Start, range.End, ct);
        var expenseRows = await reportingRepository.GetExpenseAggregatesAsync(new ReportScope(null, projectId), range.Start, range.End, ct);
        var currency = await ResolveProjectCurrencyAsync(projectId, ct);
        var projectCurrency = await ResolveNativeProjectCurrencyAsync(projectId, ct);
        var project = await projects.GetByIdAsync(projectId, ct);

        var byTeam = new List<ProfitByTeamLine>();
        foreach (var g in rows.GroupBy(r => (r.TeamId, r.TeamName)))
        {
            var cost = await currencyConversion.ConvertAsync(new Money(g.Sum(x => x.CostAmountNative), projectCurrency), currency, range.End, ct);

            decimal billedAmount;
            if (project?.PaymentModel == PaymentModel.FixedProjectCost)
            {
                billedAmount = 0;
            }
            else
            {
                var billed = await currencyConversion.ConvertAsync(new Money(g.Sum(x => x.BilledAmountNative), projectCurrency), currency, range.End, ct);
                billedAmount = billed.Amount;
            }

            byTeam.Add(new ProfitByTeamLine(g.Key.TeamId, g.Key.TeamName, billedAmount, cost.Amount, billedAmount - cost.Amount, g.Sum(x => x.EntryCount)));
        }

        var expenseCost = await SumConvertedAsync(expenseRows.Select(e => (e.Amount, e.Currency, e.Date)), currency, ct);
        var totalHours = rows.Sum(r => r.WorkHours + r.OutOfHoursHours);
        var totalCost = byTeam.Sum(l => l.Cost) + expenseCost;

        var totalBilled = project?.PaymentModel == PaymentModel.FixedProjectCost
            ? await revenueRecognition.GetRecognizedRevenueAsync(projectId, totalHours, currency, range.End, ct)
            : byTeam.Sum(l => l.Billed);

        var profit = totalBilled - totalCost;
        var margin = totalBilled == 0 ? 0 : Math.Round(profit / totalBilled * 100, 2);
        return new ReportEnvelope<ProfitSummary, ProfitByTeamLine>(
            range, currency, new ProfitSummary(totalBilled, totalCost, profit, margin, rows.Sum(r => r.EntryCount)), byTeam.OrderByDescending(l => l.Profit).ToList());
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
            var projectHours = projectGroup.Sum(x => x.WorkHours + x.OutOfHoursHours);

            var costNative = projectGroup.Sum(x => x.CostAmountNative);
            var billedNative = project?.PaymentModel == PaymentModel.FixedProjectCost ? 0 : projectGroup.Sum(x => x.BilledAmountNative);

            var cost = await currencyConversion.ConvertAsync(new Money(costNative, projectCurrency), currency, range.End, ct);
            var expenseCost = await SumConvertedAsync(
                expenseRows.Where(e => e.ProjectId == projectGroup.Key.ProjectId).Select(e => (e.Amount, e.Currency, e.Date)), currency, ct);

            var billed = project?.PaymentModel == PaymentModel.FixedProjectCost
                ? await revenueRecognition.GetRecognizedRevenueAsync(projectGroup.Key.ProjectId, projectHours, currency, range.End, ct)
                : (await currencyConversion.ConvertAsync(new Money(billedNative, projectCurrency), currency, range.End, ct)).Amount;

            var totalCost = cost.Amount + expenseCost;
            var profit = billed - totalCost;
            var margin = billed == 0 ? 0 : Math.Round(profit / billed * 100, 2);
            byProject.Add(new ProfitByProjectLine(projectGroup.Key.ProjectId, projectGroup.Key.ProjectName, billed, totalCost, profit, margin, projectGroup.Sum(x => x.EntryCount)));
        }

        var summaryBilled = byProject.Sum(l => l.Billed);
        var summaryCost = byProject.Sum(l => l.Cost);
        var summaryProfit = summaryBilled - summaryCost;
        var summaryMargin = summaryBilled == 0 ? 0 : Math.Round(summaryProfit / summaryBilled * 100, 2);

        return new ReportEnvelope<ProfitSummary, ProfitByProjectLine>(
            range, currency, new ProfitSummary(summaryBilled, summaryCost, summaryProfit, summaryMargin, rows.Sum(r => r.EntryCount)),
            byProject.OrderByDescending(l => l.Profit).ToList());
    }

    private static TimeSummary SummarizeTime(IReadOnlyList<TimeEntryAggregateRow> rows, ProjectStatus? status = null) => new(
        rows.Sum(r => r.WorkHours), rows.Sum(r => r.OutOfHoursHours), rows.Sum(r => r.WorkHours + r.OutOfHoursHours), rows.Sum(r => r.EntryCount),
        status?.BudgetHours, HoursRemaining(status?.BudgetHours, status?.ActualHours));

    private static decimal? HoursRemaining(decimal? budgetHours, decimal? actualHours) =>
        budgetHours.HasValue ? budgetHours.Value - (actualHours ?? 0) : null;

    /// <summary>Null-safe wrapper for a report method's own project fetch + IProjectStatusService call - a
    /// project that's somehow gone (deleted mid-request) just means no budget figures on this report, not a
    /// failed report.</summary>
    private async Task<ProjectStatus?> GetProjectStatusAsync(int projectId, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(projectId, ct);
        return project is null ? null : await projectStatus.GetStatusAsync(project, ct);
    }

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
