using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Infrastructure.Data;
using TimeSheet.Infrastructure.Repositories;
using TimeSheet.Infrastructure.Services;
using Xunit;

namespace TimeSheet.Infrastructure.Tests;

public class PayrollAggregationServiceTests
{
    private static readonly DateOnly PeriodStart = new(2026, 1, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 1, 31);

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

    private static async Task<(Client Client, Project Project, User User)> SeedAsync(TimesheetDbContext db)
    {
        var client = new Client { Name = "Antigua", AccountCode = "C1", StartDate = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow };
        db.Clients.Add(client);
        var user = new User { Email = "staff@svgit.co.uk", DisplayName = "Staff", PayrollNumber = "P0001", CreatedUtc = DateTimeOffset.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var project = new Project
        {
            ClientId = client.Id, Name = "Project A", Code = "A", PaymentModel = PaymentModel.TimeAndMaterials,
            StartDate = new DateOnly(2026, 1, 1), IsActive = true, CreatedUtc = DateTimeOffset.UtcNow,
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return (client, project, user);
    }

    private static TimesheetEntry MakeEntry(
        Client client, Project project, User user, decimal outOfHoursHours, decimal? resolvedOutOfHoursCost,
        bool approvedPayroll = true, bool sentToPayroll = false, DateOnly? date = null) => new()
    {
        UserId = user.Id, ClientId = client.Id, ProjectId = project.Id,
        Date = date ?? new DateOnly(2026, 1, 10), WorkHours = 0, OutOfHoursHours = outOfHoursHours, Description = "OOH work",
        ResolvedOutOfHoursCost = resolvedOutOfHoursCost,
        // Deliberately absurd - proves the service never sums this instead of OutOfHoursHours * ResolvedOutOfHoursCost.
        ToPayroll = 99999m, ToCompany = 99999m,
        ApprovedPayroll = approvedPayroll, SentToPayroll = sentToPayroll,
        CreatedUtc = DateTimeOffset.UtcNow,
    };

    private static PayrollAggregationService CreateService(TimesheetDbContext db) =>
        new(new TimesheetEntryRepository(db), new PayrollPeriodRepository(db), db);

    [Fact]
    public async Task AggregateAsync_TwoEntriesSameUser_SummedUsingResolvedOutOfHoursCostNotToPayroll()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedAsync(db);
        db.TimesheetEntries.AddRange(
            MakeEntry(client, project, user, outOfHoursHours: 4m, resolvedOutOfHoursCost: 20m),
            MakeEntry(client, project, user, outOfHoursHours: 2m, resolvedOutOfHoursCost: 25m));
        await db.SaveChangesAsync();

        var period = await CreateService(db).AggregateAsync(PeriodStart, PeriodEnd, CancellationToken.None);

        var line = Assert.Single(period.Lines);
        Assert.Equal(user.Id, line.UserId);
        Assert.Equal(6m, line.OutOfHoursHours);
        Assert.Equal(4m * 20m + 2m * 25m, line.OutOfHoursPay); // 130, nowhere near ToPayroll's 99999
        Assert.Equal(6m, period.TotalOutOfHoursHours);
        Assert.Equal(130m, period.TotalOutOfHoursPay);
    }

    [Fact]
    public async Task AggregateAsync_ZeroOutOfHours_Excluded()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedAsync(db);
        db.TimesheetEntries.Add(MakeEntry(client, project, user, outOfHoursHours: 0m, resolvedOutOfHoursCost: 20m));
        await db.SaveChangesAsync();

        var period = await CreateService(db).AggregateAsync(PeriodStart, PeriodEnd, CancellationToken.None);

        Assert.Empty(period.Lines);
    }

    [Fact]
    public async Task AggregateAsync_NotApproved_Excluded()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedAsync(db);
        db.TimesheetEntries.Add(MakeEntry(client, project, user, outOfHoursHours: 4m, resolvedOutOfHoursCost: 20m, approvedPayroll: false));
        await db.SaveChangesAsync();

        var period = await CreateService(db).AggregateAsync(PeriodStart, PeriodEnd, CancellationToken.None);

        Assert.Empty(period.Lines);
    }

    [Fact]
    public async Task AggregateAsync_AlreadySentToPayroll_Excluded()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedAsync(db);
        db.TimesheetEntries.Add(MakeEntry(client, project, user, outOfHoursHours: 4m, resolvedOutOfHoursCost: 20m, sentToPayroll: true));
        await db.SaveChangesAsync();

        var period = await CreateService(db).AggregateAsync(PeriodStart, PeriodEnd, CancellationToken.None);

        Assert.Empty(period.Lines);
    }

    [Fact]
    public async Task AggregateAsync_OutsidePeriod_Excluded()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedAsync(db);
        db.TimesheetEntries.Add(MakeEntry(client, project, user, outOfHoursHours: 4m, resolvedOutOfHoursCost: 20m, date: new DateOnly(2026, 2, 1)));
        await db.SaveChangesAsync();

        var period = await CreateService(db).AggregateAsync(PeriodStart, PeriodEnd, CancellationToken.None);

        Assert.Empty(period.Lines);
    }

    [Fact]
    public async Task AggregateAsync_ReRunSamePeriod_DoesNotDuplicateRows()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedAsync(db);
        db.TimesheetEntries.Add(MakeEntry(client, project, user, outOfHoursHours: 4m, resolvedOutOfHoursCost: 20m));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var first = await service.AggregateAsync(PeriodStart, PeriodEnd, CancellationToken.None);
        var second = await service.AggregateAsync(PeriodStart, PeriodEnd, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(second.Lines);
        Assert.Equal(1, await db.PayrollPeriods.CountAsync());
        Assert.Equal(1, await db.PayrollPeriodLines.CountAsync());
    }

    [Fact]
    public async Task AggregateAsync_NeverMutatesSourceEntries()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedAsync(db);
        var entry = MakeEntry(client, project, user, outOfHoursHours: 4m, resolvedOutOfHoursCost: 20m);
        db.TimesheetEntries.Add(entry);
        await db.SaveChangesAsync();

        await CreateService(db).AggregateAsync(PeriodStart, PeriodEnd, CancellationToken.None);

        var reloaded = await db.TimesheetEntries.AsNoTracking().SingleAsync(e => e.Id == entry.Id);
        Assert.True(reloaded.ApprovedPayroll);
        Assert.False(reloaded.SentToPayroll);
    }
}
