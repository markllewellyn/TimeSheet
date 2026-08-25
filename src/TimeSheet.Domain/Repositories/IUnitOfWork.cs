namespace TimeSheet.Domain.Repositories;

/// <summary>Repositories track changes against the DbContext; nothing commits on its own so multi-entity
/// operations (e.g. "create Project + seed its first default ProjectRate") stay atomic.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
