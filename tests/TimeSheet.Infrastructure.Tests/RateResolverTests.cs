using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Services;
using TimeSheet.Infrastructure.Data;
using TimeSheet.Infrastructure.Repositories;
using TimeSheet.Infrastructure.Services;
using Xunit;

namespace TimeSheet.Infrastructure.Tests;

public class RateResolverTests
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

    private static async Task<(Client Client, Project Project, User User)> SeedClientProjectUserAsync(TimesheetDbContext db, int? jobRoleId = null)
    {
        var client = new Client { Name = "Antigua", AccountCode = "C1", StartDate = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var project = new Project
        {
            ClientId = client.Id, Name = "Project A", Code = "A", PaymentModel = PaymentModel.TimeAndMaterials,
            StartDate = new DateOnly(2026, 1, 1), IsActive = true, CreatedUtc = DateTimeOffset.UtcNow,
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var user = new User
        {
            EntraObjectId = "oid-1", Email = "test@svgit.co.uk", DisplayName = "Test User",
            PayrollNumber = "P0001", Role = UserRole.User, JobRoleId = jobRoleId, CreatedUtc = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (client, project, user);
    }

    private static RateResolver CreateResolver(TimesheetDbContext db) =>
        new(new RateCardRepository(db), new StaffCostRepository(db), new UserRepository(db));

    [Fact]
    public async Task ResolveAsync_PersonClientOverride_WinsOverNoRole()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedClientProjectUserAsync(db);

        db.StaffCosts.Add(new StaffCost { StaffId = user.Id, HourlyCost = 40m, OutOfHoursCost = 5m, EffectiveFrom = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow });
        db.RateCards.Add(new RateCard { StaffId = user.Id, ClientId = client.Id, Rate = 100m, EffectiveFrom = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var resolution = await CreateResolver(db).ResolveAsync(user.Id, client.Id, project.Id, new DateOnly(2026, 8, 10), false, CancellationToken.None);

        Assert.Equal(100m, resolution.CustomerRate);
        Assert.Equal(40m, resolution.HourlyCost);
        Assert.Equal(5m, resolution.OutOfHoursCost);
        Assert.Equal(RateCardTier.PersonClient, resolution.Tier);
    }

    [Fact]
    public async Task ResolveAsync_NoPersonOverride_FallsBackToRoleDefault()
    {
        await using var db = CreateInMemoryDb();

        var role = new Role { Name = "Developer", IsActive = true, CreatedUtc = DateTimeOffset.UtcNow };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var (client, project, user) = await SeedClientProjectUserAsync(db, role.Id);

        db.StaffCosts.Add(new StaffCost { StaffId = user.Id, HourlyCost = 30m, OutOfHoursCost = 0m, EffectiveFrom = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow });
        db.RateCards.Add(new RateCard { RoleId = role.Id, Rate = 75m, EffectiveFrom = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var resolution = await CreateResolver(db).ResolveAsync(user.Id, client.Id, project.Id, new DateOnly(2026, 8, 10), false, CancellationToken.None);

        Assert.Equal(75m, resolution.CustomerRate);
        Assert.Equal(RateCardTier.RoleDefault, resolution.Tier);
    }

    [Fact]
    public async Task ResolveAsync_NoMatchingRateCard_ThrowsRateNotConfigured()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedClientProjectUserAsync(db);

        db.StaffCosts.Add(new StaffCost { StaffId = user.Id, HourlyCost = 40m, OutOfHoursCost = 0m, EffectiveFrom = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<RateNotConfiguredException>(() =>
            CreateResolver(db).ResolveAsync(user.Id, client.Id, project.Id, new DateOnly(2026, 8, 10), false, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_RateCardWithDiscount_ReducesCustomerRateButNotCost()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedClientProjectUserAsync(db);

        db.StaffCosts.Add(new StaffCost { StaffId = user.Id, HourlyCost = 40m, OutOfHoursCost = 5m, EffectiveFrom = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow });
        db.RateCards.Add(new RateCard { StaffId = user.Id, ClientId = client.Id, Rate = 100m, DiscountPercent = 10m, EffectiveFrom = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var resolution = await CreateResolver(db).ResolveAsync(user.Id, client.Id, project.Id, new DateOnly(2026, 8, 10), false, CancellationToken.None);

        Assert.Equal(90m, resolution.CustomerRate);
        Assert.Equal(40m, resolution.HourlyCost);
    }

    [Fact]
    public async Task ResolveAsync_FutureDatedRateCard_IsNotPickedForAPastDate()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedClientProjectUserAsync(db);

        db.StaffCosts.Add(new StaffCost { StaffId = user.Id, HourlyCost = 40m, OutOfHoursCost = 0m, EffectiveFrom = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow });
        db.RateCards.Add(new RateCard { StaffId = user.Id, ClientId = client.Id, Rate = 100m, EffectiveFrom = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow });
        db.RateCards.Add(new RateCard { StaffId = user.Id, ClientId = client.Id, Rate = 200m, EffectiveFrom = new DateOnly(2026, 12, 1), CreatedUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var resolution = await CreateResolver(db).ResolveAsync(user.Id, client.Id, project.Id, new DateOnly(2026, 8, 10), false, CancellationToken.None);

        Assert.Equal(100m, resolution.CustomerRate);
    }

    [Fact]
    public async Task ResolveAsync_CostExemptProject_ReturnsZeroCostWithoutStaffCost()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedClientProjectUserAsync(db);

        // Deliberately no StaffCost seeded - a cost-exempt project must not need one.
        db.RateCards.Add(new RateCard { StaffId = user.Id, ClientId = client.Id, Rate = 100m, EffectiveFrom = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var resolution = await CreateResolver(db).ResolveAsync(user.Id, client.Id, project.Id, new DateOnly(2026, 8, 10), true, CancellationToken.None);

        Assert.Equal(100m, resolution.CustomerRate);
        Assert.Equal(0m, resolution.HourlyCost);
        Assert.Equal(0m, resolution.OutOfHoursCost);
        Assert.Null(resolution.StaffCostId);
    }

    [Fact]
    public async Task ResolveAsync_NotCostExempt_NoStaffCost_ThrowsRateNotConfigured()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedClientProjectUserAsync(db);

        // Same seed as the cost-exempt test above (RateCard, no StaffCost) but isCostExempt: false - confirms
        // the missing-StaffCost throw path (previously untested) still fires when a project isn't exempt.
        db.RateCards.Add(new RateCard { StaffId = user.Id, ClientId = client.Id, Rate = 100m, EffectiveFrom = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<RateNotConfiguredException>(() =>
            CreateResolver(db).ResolveAsync(user.Id, client.Id, project.Id, new DateOnly(2026, 8, 10), false, CancellationToken.None));
    }
}
