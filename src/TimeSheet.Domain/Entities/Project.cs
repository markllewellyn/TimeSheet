namespace TimeSheet.Domain.Entities;

public class Project
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public Client? Client { get; set; }

    public required string Name { get; set; }

    /// <summary>Unique per-client, not globally (mirrors the legacy app's project codes, e.g. "FDD-3145").</summary>
    public required string Code { get; set; }
    public string? Description { get; set; }

    public PaymentModel PaymentModel { get; set; }

    /// <summary>Project-level currency override. When null, the project inherits Client.ReportingCurrencyCode.</summary>
    public string? CurrencyOverride { get; set; }

    public DateOnly StartDate { get; set; }

    /// <summary>Null = open-ended / retainer engagement.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>The "time limit" budget ceiling in hours (either payment model).</summary>
    public decimal? BudgetHours { get; set; }

    /// <summary>Only meaningful when PaymentModel == FixedProjectCost.</summary>
    public decimal? FixedFeeAmount { get; set; }

    /// <summary>Percentage of BudgetHours consumed that triggers a non-blocking "approaching budget" warning.</summary>
    public int BudgetAlertThresholdPercent { get; set; } = 80;

    public bool IsActive { get; set; } = true;

    /// <summary>Denormalized pointer to the latest AI health verdict, avoiding a correlated MAX() subquery on dashboard reads.</summary>
    public int? LatestHealthAssessmentId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? ModifiedUtc { get; set; }

    public List<ProjectRate> Rates { get; set; } = [];
    public List<ProjectAssignment> Assignments { get; set; } = [];

    public string GetEffectiveCurrency(string clientReportingCurrency) => CurrencyOverride ?? clientReportingCurrency;
}
