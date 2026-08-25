using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class UserRepository(TimesheetDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEntraObjectIdAsync(string entraObjectId, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.EntraObjectId == entraObjectId, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<IReadOnlyList<User>> GetAllAsync(bool includeInactive, CancellationToken ct)
    {
        var query = db.Users.AsQueryable();
        if (!includeInactive) query = query.Where(u => u.IsActive);
        return await query.OrderBy(u => u.DisplayName).ToListAsync(ct);
    }

    public async Task AddAsync(User user, CancellationToken ct) => await db.Users.AddAsync(user, ct);

    public void Update(User user) => db.Users.Update(user);
}
