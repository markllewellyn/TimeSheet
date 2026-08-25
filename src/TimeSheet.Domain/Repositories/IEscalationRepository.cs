using TimeSheet.Domain.Entities;

namespace TimeSheet.Domain.Repositories;

public interface IEscalationRepository
{
    Task<Escalation?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<Escalation>> GetPendingAsync(CancellationToken ct);
    Task AddAsync(Escalation escalation, CancellationToken ct);
    void Update(Escalation escalation);
}
