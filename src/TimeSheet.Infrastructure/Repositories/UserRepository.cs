using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class UserRepository(TimesheetDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Users.Include(u => u.JobRole).Include(u => u.Team).FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEntraObjectIdAsync(string entraObjectId, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.EntraObjectId == entraObjectId, ct);

    // Case-insensitive: an email's casing isn't something callers (a local-login form, an Entra token's
    // preferred_username claim) can be relied on to match byte-for-byte against however it was originally
    // typed into this table - e.g. Entra returned "Mark.Llewellyn@svgit.co.uk" for a row stored as
    // "mark.llewellyn@svgit.co.uk", which a plain == silently never matches (SQLite string comparison is
    // case-sensitive by default).
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), ct);

    public async Task<IReadOnlyList<User>> GetAllAsync(bool includeInactive, CancellationToken ct)
    {
        var query = db.Users.Include(u => u.JobRole).Include(u => u.Team).AsQueryable();
        if (!includeInactive) query = query.Where(u => u.IsActive);
        return await query.OrderBy(u => u.DisplayName).ToListAsync(ct);
    }

    public async Task AddAsync(User user, CancellationToken ct) => await db.Users.AddAsync(user, ct);

    public void Update(User user) => db.Users.Update(user);
}
