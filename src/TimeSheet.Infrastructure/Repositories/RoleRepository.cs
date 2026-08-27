using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class RoleRepository(TimesheetDbContext db) : IRoleRepository
{
    public Task<Role?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<Role?> GetByNameAsync(string name, CancellationToken ct) =>
        db.Roles.FirstOrDefaultAsync(r => r.Name == name, ct);

    public async Task<IReadOnlyList<Role>> GetAllAsync(bool includeInactive, CancellationToken ct)
    {
        var query = db.Roles.AsQueryable();
        if (!includeInactive) query = query.Where(r => r.IsActive);
        return await query.OrderBy(r => r.Name).ToListAsync(ct);
    }

    public async Task AddAsync(Role role, CancellationToken ct) => await db.Roles.AddAsync(role, ct);

    public void Update(Role role) => db.Roles.Update(role);
}
