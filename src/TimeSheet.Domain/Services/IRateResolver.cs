namespace TimeSheet.Domain.Services;

public record RateSnapshot(decimal CostRatePerHour, decimal? BillingRatePerHour, int ProjectRateId);

public interface IRateResolver
{
    /// <summary>Resolves the effective ProjectRate for (project, user, date) per IProjectRateRepository's
    /// most-specific-first algorithm. Throws RateNotConfiguredException if nothing resolves — a data-entry gap,
    /// never a silent zero.</summary>
    Task<RateSnapshot> GetEffectiveRateAsync(int projectId, int userId, DateOnly onDate, CancellationToken ct);

    /// <summary>Bulk variant for reporting: resolves many (project, user) pairs as of a single date in one pass.</summary>
    Task<IReadOnlyDictionary<(int ProjectId, int UserId), RateSnapshot>> GetRatesAsync(
        IEnumerable<(int ProjectId, int UserId)> pairs, DateOnly asOf, CancellationToken ct);
}

public class RateNotConfiguredException(int projectId, int? userId)
    : Exception($"No ProjectRate configured for project {projectId}" + (userId is null ? "" : $", user {userId}") + ".");
