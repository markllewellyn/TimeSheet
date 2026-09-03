using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Services;

public record ProjectHealthAnalysisInput(
    string ProjectName, string ClientName, string PaymentModel,
    decimal? BudgetHours, decimal? FixedFeeAmount, DateOnly StartDate, DateOnly? PlannedEndDate,
    decimal TotalHoursLoggedToDate, decimal HoursLoggedLast7Days, decimal HoursLoggedPrevious7Days, decimal HoursLoggedLast30Days,
    decimal? PercentBudgetConsumed, decimal? PercentTimeElapsed,
    IReadOnlyList<string> RecentEntryDescriptions);

public record ProjectHealthVerdict(ProjectHealthStatus Status, string Summary, IReadOnlyList<string> ContributingFactors, string? RecommendedAction);

/// <summary>Wraps the AI provider call. Claude Sonnet 5 is the plan's recommendation - a plain-language,
/// admin-facing explanation of *why* a project looks unhealthy is a qualitative-reasoning/writing task, and
/// structured output is requested via strict tool-use (forced tool_choice), never parsed from free-text.</summary>
public interface IHealthAnalysisClient
{
    Task<ProjectHealthVerdict> AnalyzeAsync(ProjectHealthAnalysisInput input, CancellationToken ct);
}

/// <summary>Gathers the project's current data, calls the AI, and maps the verdict to an (unpersisted)
/// ProjectHealthAssessment - IProjectHealthService owns persistence/notification.</summary>
public interface IProjectHealthAssessor
{
    Task<ProjectHealthAssessment> AssessAsync(int projectId, CancellationToken ct);
}

public record ProjectHealthSweepResult(int Assessed, int Failed);

public interface IProjectHealthService
{
    Task<ProjectHealthAssessment?> GetLatestAsync(int projectId, CancellationToken ct);
    Task<IReadOnlyList<ProjectHealthAssessment>> GetHistoryAsync(int projectId, CancellationToken ct);

    /// <summary>All active projects that have at least one assessment, worst status first.</summary>
    Task<IReadOnlyList<ProjectHealthAssessment>> GetDashboardAsync(CancellationToken ct);

    /// <summary>Assesses, persists, and - only when the status worsens (or on-demand is explicitly
    /// requested) - notifies Admins. Avoids alert fatigue: a nightly run that stays OnTrack raises nothing.</summary>
    Task<ProjectHealthAssessment> ReassessAsync(int projectId, CancellationToken ct);

    /// <summary>Reassesses every active project (bounded concurrency, respecting the AI provider's rate
    /// limits) - the shared logic behind both NightlyProjectHealthAssessment and the manual "Run Now" trigger,
    /// so the two are guaranteed to behave identically. A single project's failure (e.g. a missing/invalid AI
    /// provider API key) never aborts the sweep for the rest - see ReassessAsync's own error behavior.</summary>
    Task<ProjectHealthSweepResult> ReassessAllActiveAsync(CancellationToken ct);
}
