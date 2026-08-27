namespace TimeSheet.Domain.Repositories;

/// <summary>Repositories track changes against the DbContext; nothing commits on its own so multi-entity
/// operations (e.g. "update a StaffCost + write its StaffCostHistory audit row") stay atomic.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
