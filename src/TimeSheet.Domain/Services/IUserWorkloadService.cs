namespace TimeSheet.Domain.Services;

public record ProjectAllocationDto(int ProjectId, string ProjectName, string ClientName, decimal AllocatedHoursPerWeek);

public record EstimatedWeeklyWorkload(
    decimal EstimatedHoursThisWeek,
    IReadOnlyList<ProjectAllocationDto> ByProject,
    IReadOnlyList<int> AssignmentsMissingAllocation);

public interface IUserWorkloadService
{
    /// <summary>Sums AllocatedHoursPerWeek across the user's Active assignments overlapping the target week.
    /// Assignments missing an allocation are surfaced separately so the total isn't silently understated.</summary>
    Task<EstimatedWeeklyWorkload> GetEstimatedHoursThisWeekAsync(int userId, DateOnly weekStart, CancellationToken ct);
}
