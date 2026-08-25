using TimeSheet.Domain.Repositories;
using TimeSheet.Domain.Services;

namespace TimeSheet.Infrastructure.Services;

public class RateResolver(IProjectRateRepository rates) : IRateResolver
{
    public async Task<RateSnapshot> GetEffectiveRateAsync(int projectId, int userId, DateOnly onDate, CancellationToken ct)
    {
        var rate = await rates.GetEffectiveRateAsync(projectId, userId, onDate, ct)
            ?? throw new RateNotConfiguredException(projectId, userId);
        return new RateSnapshot(rate.CostRatePerHour, rate.BillingRatePerHour, rate.Id);
    }

    // Per-pair queries are fine at this app's local-SQLite scale (bounded by distinct project/user combinations
    // in a report, not raw row count); revisit with a single batched query if profiling ever shows it matters.
    public async Task<IReadOnlyDictionary<(int ProjectId, int UserId), RateSnapshot>> GetRatesAsync(
        IEnumerable<(int ProjectId, int UserId)> pairs, DateOnly asOf, CancellationToken ct)
    {
        var result = new Dictionary<(int, int), RateSnapshot>();
        foreach (var (projectId, userId) in pairs.Distinct())
        {
            result[(projectId, userId)] = await GetEffectiveRateAsync(projectId, userId, asOf, ct);
        }
        return result;
    }
}
