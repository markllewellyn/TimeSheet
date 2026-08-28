using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Infrastructure.Data;
using TimeSheet.Infrastructure.Repositories;
using TimeSheet.Infrastructure.Services;
using Xunit;

namespace TimeSheet.Infrastructure.Tests;

public class BudgetMonitoringServiceTests
{
    private static TimesheetDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<TimesheetDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var db = new TimesheetDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated(); // also applies AppSettingsConfiguration's HasData seed (Id=1, 50/75 defaults)
        return db;
    }

    private static async Task<Project> SeedProjectAsync(TimesheetDbContext db, decimal budgetHours)
    {
        var client = new Client { Name = "Antigua", AccountCode = "C1", StartDate = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var project = new Project
        {
            ClientId = client.Id, Name = "Project A", Code = "A", PaymentModel = PaymentModel.TimeAndMaterials,
            StartDate = new DateOnly(2026, 1, 1), IsActive = true, BudgetHours = budgetHours, CreatedUtc = DateTimeOffset.UtcNow,
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    private static BudgetMonitoringService CreateService(TimesheetDbContext db) =>
        new(new ProjectRepository(db), new TimesheetEntryRepository(db), new AppSettingsRepository(db));

    private static TimesheetEntry MakeEntry(Project project, decimal workHours) => new()
    {
        UserId = 1, ClientId = project.ClientId, ProjectId = project.Id,
        Date = new DateOnly(2026, 1, 5), WorkHours = workHours, OutOfHoursHours = 0, Description = "work",
    };

    [Fact]
    public async Task EvaluateAsync_BelowWarningThreshold_NoCrossingAndNoEscalation()
    {
        await using var db = CreateInMemoryDb();
        var project = await SeedProjectAsync(db, budgetHours: 100m);

        var result = await CreateService(db).EvaluateAsync(MakeEntry(project, 30m), CancellationToken.None);

        Assert.False(result.RequiresEscalation);
        Assert.Null(result.NewlyCrossedNotificationThresholdPercent);
    }

    [Fact]
    public async Task EvaluateAsync_CrossesDefaultWarningThreshold_ReturnsFifty()
    {
        await using var db = CreateInMemoryDb();
        var project = await SeedProjectAsync(db, budgetHours: 100m);

        var result = await CreateService(db).EvaluateAsync(MakeEntry(project, 55m), CancellationToken.None);

        Assert.False(result.RequiresEscalation);
        Assert.Equal(50, result.NewlyCrossedNotificationThresholdPercent);
    }

    [Fact]
    public async Task EvaluateAsync_JumpsStraightPastAlertThreshold_ReturnsSeventyFiveNotFifty()
    {
        await using var db = CreateInMemoryDb();
        var project = await SeedProjectAsync(db, budgetHours: 100m);

        // Nothing logged yet (HighestBudgetNotificationPercent is null) - one entry jumps straight to 80%.
        var result = await CreateService(db).EvaluateAsync(MakeEntry(project, 80m), CancellationToken.None);

        Assert.Equal(75, result.NewlyCrossedNotificationThresholdPercent);
    }

    [Fact]
    public async Task EvaluateAsync_AlreadyNotifiedAtWarning_DoesNotRenotifyUntilAlertThreshold()
    {
        await using var db = CreateInMemoryDb();
        var project = await SeedProjectAsync(db, budgetHours: 100m);
        project.HighestBudgetNotificationPercent = 50;
        db.Projects.Update(project);

        var user = new User { Email = "staff@svgit.co.uk", DisplayName = "Staff", PayrollNumber = "P0001", CreatedUtc = DateTimeOffset.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Existing 55 hours logged elsewhere + a new 10-hour entry = 65% - still under the 75% alert.
        db.TimesheetEntries.Add(new TimesheetEntry
        {
            UserId = user.Id, ClientId = project.ClientId, ProjectId = project.Id,
            Date = new DateOnly(2026, 1, 1), WorkHours = 55m, OutOfHoursHours = 0, Description = "prior work",
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).EvaluateAsync(MakeEntry(project, 10m), CancellationToken.None);

        Assert.Null(result.NewlyCrossedNotificationThresholdPercent);
    }

    [Fact]
    public async Task EvaluateAsync_OverBudget_RequiresEscalationAndNoNotificationCrossing()
    {
        await using var db = CreateInMemoryDb();
        var project = await SeedProjectAsync(db, budgetHours: 100m);

        var result = await CreateService(db).EvaluateAsync(MakeEntry(project, 110m), CancellationToken.None);

        Assert.True(result.RequiresEscalation);
        Assert.Null(result.NewlyCrossedNotificationThresholdPercent);
    }

    [Fact]
    public async Task EvaluateAsync_UsesConfiguredThresholds_NotHardcodedFiftySeventyFive()
    {
        await using var db = CreateInMemoryDb();
        var project = await SeedProjectAsync(db, budgetHours: 100m);

        var settings = await db.AppSettings.FirstAsync(s => s.Id == 1);
        settings.ProjectBudgetWarningThresholdPercent = 20;
        settings.ProjectBudgetAlertThresholdPercent = 40;
        await db.SaveChangesAsync();

        // 25% would not cross the default 50% threshold, but does cross this project's configured 20%.
        var result = await CreateService(db).EvaluateAsync(MakeEntry(project, 25m), CancellationToken.None);

        Assert.Equal(20, result.NewlyCrossedNotificationThresholdPercent);
    }

    [Fact]
    public async Task EvaluateAsync_NoBudgetHours_ReturnsNoEscalationOrCrossing()
    {
        await using var db = CreateInMemoryDb();
        var project = await SeedProjectAsync(db, budgetHours: 0m);
        project.BudgetHours = null;
        db.Projects.Update(project);
        await db.SaveChangesAsync();

        var result = await CreateService(db).EvaluateAsync(MakeEntry(project, 1000m), CancellationToken.None);

        Assert.False(result.RequiresEscalation);
        Assert.Null(result.NewlyCrossedNotificationThresholdPercent);
    }
}
