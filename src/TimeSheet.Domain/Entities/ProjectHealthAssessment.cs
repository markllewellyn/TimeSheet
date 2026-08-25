namespace TimeSheet.Domain.Entities;

public class ProjectHealthAssessment
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public DateTimeOffset AssessedAtUtc { get; set; }
    public ProjectHealthStatus Status { get; set; }
    public required string Summary { get; set; }

    /// <summary>Serialized List&lt;string&gt; of contributing factors returned by the AI's structured output.</summary>
    public string ContributingFactorsJson { get; set; } = "[]";

    public string? RecommendedAction { get; set; }
    public decimal? PercentBudgetConsumed { get; set; }
    public decimal? PercentTimeElapsed { get; set; }

    /// <summary>Audit/repro, e.g. "claude-sonnet-5".</summary>
    public required string AiModelUsed { get; set; }

    public bool NotificationSent { get; set; }
}
