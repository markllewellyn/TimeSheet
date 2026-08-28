using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;
using TimeSheet.Infrastructure.Data;

namespace TimeSheet.Infrastructure.Repositories;

public class AppSettingsRepository(TimesheetDbContext db) : IAppSettingsRepository
{
    public Task<AppSettings> GetAsync(CancellationToken ct) => db.AppSettings.FirstAsync(s => s.Id == 1, ct);

    public void Update(AppSettings settings) => db.AppSettings.Update(settings);
}
