using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IRateCardRepository
{
    /// <summary>Exact-scope lookup for one resolution tier: the row matching this precise (roleId, staffId,
    /// clientId, projectId) combination (nulls matched as nulls) with the highest EffectiveFrom &lt;= asOfDate -
    /// null if no row exists for this exact scope. See RateResolver, which calls this once per tier.</summary>
    Task<RateCard?> FindAsync(int? roleId, int? staffId, int? clientId, int? projectId, DateOnly asOfDate, CancellationToken ct);

    /// <summary>Loose admin-browsing filter: each non-null parameter narrows the result, unlike FindAsync's
    /// exact-null-match semantics. Newest first.</summary>
    Task<IReadOnlyList<RateCard>> ListAsync(int? staffId, int? roleId, int? clientId, int? projectId, CancellationToken ct);

    Task AddAsync(RateCard rateCard, CancellationToken ct);
}
