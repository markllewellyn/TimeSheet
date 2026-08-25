using System.Text.Json;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class ProjectHealthAssessor(IProjectRepository projects, ITimesheetEntryRepository timesheetEntries, IHealthAnalysisClient healthAnalysisClient) : IProjectHealthAssessor
{
    private const string ModelUsed = "claude-sonnet-5";

    public async Task<ProjectHealthAssessment> AssessAsync(int projectId, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(projectId, ct)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
        var entries = await timesheetEntries.GetCountedForProjectAsync(project.Id, project.StartDate, today, ct);

        var totalHours = entries.Sum(e => e.WorkHours + e.OutOfHoursHours);
        var last7 = entries.Where(e => e.Date > today.AddDays(-7)).Sum(e => e.WorkHours + e.OutOfHoursHours);
        var prev7 = entries.Where(e => e.Date > today.AddDays(-14) && e.Date <= today.AddDays(-7)).Sum(e => e.WorkHours + e.OutOfHoursHours);
        var last30 = entries.Where(e => e.Date > today.AddDays(-30)).Sum(e => e.WorkHours + e.OutOfHoursHours);

        var percentBudgetConsumed = project.BudgetHours is > 0 ? Math.Round(totalHours / project.BudgetHours.Value * 100, 1) : (decimal?)null;
        var percentTimeElapsed = project.EndDate is { } endDate && endDate > project.StartDate
            ? Math.Round((decimal)(today.DayNumber - project.StartDate.DayNumber) / (endDate.DayNumber - project.StartDate.DayNumber) * 100, 1)
            : (decimal?)null;

        var recentDescriptions = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Description))
            .OrderByDescending(e => e.Date)
            .Take(15)
            .Select(e => e.Description!)
            .ToList();

        var input = new ProjectHealthAnalysisInput(
            project.Name, project.Client?.Name ?? "", project.PaymentModel.ToString(),
            project.BudgetHours, project.FixedFeeAmount, project.StartDate, project.EndDate,
            totalHours, last7, prev7, last30, percentBudgetConsumed, percentTimeElapsed, recentDescriptions);

        var verdict = await healthAnalysisClient.AnalyzeAsync(input, ct);

        return new ProjectHealthAssessment
        {
            ProjectId = project.Id,
            AssessedAtUtc = DateTimeOffset.UtcNow,
            Status = verdict.Status,
            Summary = verdict.Summary,
            ContributingFactorsJson = JsonSerializer.Serialize(verdict.ContributingFactors),
            RecommendedAction = verdict.RecommendedAction,
            PercentBudgetConsumed = percentBudgetConsumed,
            PercentTimeElapsed = percentTimeElapsed,
            AiModelUsed = ModelUsed,
            NotificationSent = false,
        };
    }
}
