using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class ReportingRepository(TimesheetDbContext db) : IReportingRepository
{
    public async Task<IReadOnlyList<TimeEntryAggregateRow>> GetTimeEntryAggregatesAsync(ReportScope scope, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var query =
            from e in db.TimesheetEntries
            join p in db.Projects on e.ProjectId equals p.Id
            join c in db.Clients on p.ClientId equals c.Id
            join u in db.Users on e.UserId equals u.Id
            join r in db.Roles on u.JobRoleId equals r.Id into roleJoin
            from role in roleJoin.DefaultIfEmpty()
            where e.Date >= start && e.Date <= end
                  && (scope.ClientId == null || p.ClientId == scope.ClientId)
                  && (scope.ProjectId == null || p.Id == scope.ProjectId)
            group e by new
            {
                ProjectId = p.Id,
                ProjectName = p.Name,
                ClientId = c.Id,
                ClientName = c.Name,
                UserId = u.Id,
                UserName = u.DisplayName,
                RoleId = u.JobRoleId,
                RoleName = role != null ? role.Name : "Unassigned",
                e.Date,
            }
            into g
            select new TimeEntryAggregateRow(
                g.Key.ProjectId, g.Key.ProjectName, g.Key.ClientId, g.Key.ClientName,
                g.Key.UserId, g.Key.UserName, g.Key.RoleId, g.Key.RoleName, g.Key.Date,
                g.Sum(x => x.WorkHours), g.Sum(x => x.OutOfHoursHours),
                g.Sum(x => (x.WorkHours + x.OutOfHoursHours) * (x.ResolvedCustomerRate ?? 0)),
                g.Sum(x => x.WorkHours * (x.ResolvedHourlyCost ?? 0) + x.OutOfHoursHours * (x.ResolvedOutOfHoursCost ?? 0)),
                g.Count());

        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ExpenseAggregateRow>> GetExpenseAggregatesAsync(ReportScope scope, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var query =
            from e in db.ExpenseEntries
            join p in db.Projects on e.ProjectId equals p.Id
            where e.Date >= start && e.Date <= end
                  && (scope.ClientId == null || p.ClientId == scope.ClientId)
                  && (scope.ProjectId == null || p.Id == scope.ProjectId)
            select new ExpenseAggregateRow(e.ProjectId, p.ClientId, e.Date, e.Amount, e.Currency);

        return await query.ToListAsync(ct);
    }
}
