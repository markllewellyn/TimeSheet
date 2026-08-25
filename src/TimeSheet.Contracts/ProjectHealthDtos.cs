namespace TimeSheet.Contracts;

public record ProjectHealthAssessmentDto(
    int Id, int ProjectId, string ProjectName, string ClientName, DateTimeOffset AssessedAtUtc,
    string Status, string Summary, IReadOnlyList<string> ContributingFactors, string? RecommendedAction,
    decimal? PercentBudgetConsumed, decimal? PercentTimeElapsed);
