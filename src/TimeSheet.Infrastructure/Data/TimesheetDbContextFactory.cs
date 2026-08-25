using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TimeSheet.Infrastructure.Data;

/// <summary>Design-time factory so `dotnet ef migrations add` works without spinning up the full Functions host.
/// Uses a placeholder local file path — the real connection string at runtime comes from local.settings.json.</summary>
public class TimesheetDbContextFactory : IDesignTimeDbContextFactory<TimesheetDbContext>
{
    public TimesheetDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TimesheetDbContext>();
        optionsBuilder.UseSqlite("Data Source=timesheet.design.db");
        return new TimesheetDbContext(optionsBuilder.Options);
    }
}
