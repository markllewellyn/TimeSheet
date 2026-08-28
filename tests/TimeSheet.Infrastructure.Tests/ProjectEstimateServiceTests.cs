using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Infrastructure.Data;
using TimeSheet.Infrastructure.Repositories;
using TimeSheet.Infrastructure.Services;
using Xunit;

namespace TimeSheet.Infrastructure.Tests;

public class ProjectEstimateServiceTests
{
    private static TimesheetDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<TimesheetDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var db = new TimesheetDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    private static ProjectEstimateService CreateService(TimesheetDbContext db) =>
        new(new ProjectRepository(db), new StaffProjectRepository(db), new RoleRepository(db),
            new RateResolver(new RateCardRepository(db), new StaffCostRepository(db), new UserRepository(db)));

    private static async Task<(Client Client, Project Project)> SeedClientProjectAsync(TimesheetDbContext db, decimal? budgetHours)
    {
        var client = new Client { Name = "Antigua", AccountCode = "C1", StartDate = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var project = new Project
        {
            ClientId = client.Id, Name = "Project A", Code = "A", PaymentModel = PaymentModel.TimeAndMaterials,
            StartDate = new DateOnly(2026, 1, 1), BudgetAlertThresholdPercent = 80, IsActive = true,
            BudgetHours = budgetHours, CreatedUtc = DateTimeOffset.UtcNow,
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        return (client, project);
    }

    private static async Task<User> SeedStaffAsync(TimesheetDbContext db, string email, decimal hourlyCost, decimal customerRate, int clientId)
    {
        var user = new User
        {
            Email = email, DisplayName = email, PayrollNumber = $"P-{email}", Role = UserRole.User, CreatedUtc = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.StaffCosts.Add(new StaffCost { StaffId = user.Id, HourlyCost = hourlyCost, OutOfHoursCost = 0m, EffectiveFrom = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow });
        db.RateCards.Add(new RateCard { StaffId = user.Id, ClientId = clientId, Rate = customerRate, EffectiveFrom = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task EstimateAsync_NoAllocatedHours_SplitsBudgetHoursEqually()
    {
        await using var db = CreateInMemoryDb();
        var (client, project) = await SeedClientProjectAsync(db, budgetHours: 100m);
        var userA = await SeedStaffAsync(db, "a@svgit.co.uk", 40m, 100m, client.Id);
        var userB = await SeedStaffAsync(db, "b@svgit.co.uk", 40m, 100m, client.Id);

        db.StaffProjects.Add(new StaffProject { StaffId = userA.Id, ProjectId = project.Id, IsActive = true, StartDate = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow });
        db.StaffProjects.Add(new StaffProject { StaffId = userB.Id, ProjectId = project.Id, IsActive = true, StartDate = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var estimate = await CreateService(db).EstimateAsync(project.Id, CancellationToken.None);

        Assert.Equal(2, estimate.Lines.Count);
        Assert.All(estimate.Lines, l => Assert.Equal(50m, l.AllocatedHours));
        Assert.Equal(4000m, estimate.EstimatedCost); // 100 total hours * 40 cost
        Assert.Equal(10000m, estimate.EstimatedRevenue); // 100 total hours * 100 rate
        Assert.Equal(6000m, estimate.EstimatedProfit);
    }

    [Fact]
    public async Task EstimateAsync_WithAllocatedHours_WeightsSplitProportionally()
    {
        await using var db = CreateInMemoryDb();
        var (client, project) = await SeedClientProjectAsync(db, budgetHours: 100m);
        var userA = await SeedStaffAsync(db, "a@svgit.co.uk", 40m, 100m, client.Id);
        var userB = await SeedStaffAsync(db, "b@svgit.co.uk", 40m, 100m, client.Id);

        // A allocated 3x as many hours/week as B - should get 75/25 of BudgetHours, not a 50/50 split.
        db.StaffProjects.Add(new StaffProject { StaffId = userA.Id, ProjectId = project.Id, IsActive = true, AllocatedHoursPerWeek = 30m, StartDate = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow });
        db.StaffProjects.Add(new StaffProject { StaffId = userB.Id, ProjectId = project.Id, IsActive = true, AllocatedHoursPerWeek = 10m, StartDate = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var estimate = await CreateService(db).EstimateAsync(project.Id, CancellationToken.None);

        var lineA = Assert.Single(estimate.Lines, l => l.UserId == userA.Id);
        var lineB = Assert.Single(estimate.Lines, l => l.UserId == userB.Id);
        Assert.Equal(75m, lineA.AllocatedHours);
        Assert.Equal(25m, lineB.AllocatedHours);
    }

    [Fact]
    public async Task EstimateAsync_AssigneeWithNoRateCard_GetsWarningNotFailure()
    {
        await using var db = CreateInMemoryDb();
        var (client, project) = await SeedClientProjectAsync(db, budgetHours: 100m);

        var user = new User { Email = "c@svgit.co.uk", DisplayName = "No Rate", PayrollNumber = "P-C", Role = UserRole.User, CreatedUtc = DateTimeOffset.UtcNow };
        db.Users.Add(user);
        // No StaffCost/RateCard configured for this user at all.
        await db.SaveChangesAsync();

        db.StaffProjects.Add(new StaffProject { StaffId = user.Id, ProjectId = project.Id, IsActive = true, StartDate = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var estimate = await CreateService(db).EstimateAsync(project.Id, CancellationToken.None);

        var line = Assert.Single(estimate.Lines);
        Assert.NotNull(line.Warning);
        Assert.Equal(0m, line.EstimatedCost);
        Assert.Equal(0m, estimate.EstimatedCost);
    }

    [Fact]
    public async Task EstimateAsync_NoBudgetHours_ReturnsZeroWithNoLines()
    {
        await using var db = CreateInMemoryDb();
        var (_, project) = await SeedClientProjectAsync(db, budgetHours: null);

        var estimate = await CreateService(db).EstimateAsync(project.Id, CancellationToken.None);

        Assert.Empty(estimate.Lines);
        Assert.Equal(0m, estimate.EstimatedCost);
    }
}
