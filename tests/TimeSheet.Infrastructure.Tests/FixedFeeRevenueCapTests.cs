using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Services;
using TimeSheet.Infrastructure.Data;
using TimeSheet.Infrastructure.Repositories;
using TimeSheet.Infrastructure.Services;
using Xunit;

namespace TimeSheet.Infrastructure.Tests;

/// <summary>A Fixed Fee project can never recognize more than its FixedFeeAmount. Before 2026-09-29 recognition
/// was uncapped (hours / BudgetHours * fee), so logging past the budget showed more revenue than the contract was
/// worth. The cap is cumulative from the project's start, so a report range needs the hours logged before it.
/// Every scenario: £3,000 fee over 30 budget hours = £100 per hour, up to 30h.</summary>
public class FixedFeeRevenueCapTests
{
    private static TimesheetDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<TimesheetDbContext>().UseSqlite("Data Source=:memory:").Options;
        var db = new TimesheetDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    private class NoopCurrencyRateProvider : ICurrencyRateProvider
    {
        public Task<decimal?> FetchRateAsync(string baseCurrency, string quoteCurrency, DateOnly date, CancellationToken ct) =>
            throw new InvalidOperationException("Should not be called for a same-currency conversion.");
    }

    /// <summary>Seeds the project plus one entry per (user, date, hours).</summary>
    private static async Task<(Project Project, User Alice, User Bob)> SeedAsync(TimesheetDbContext db, params (bool Alice, DateOnly Date, decimal Hours)[] entries)
    {
        var client = new Client { Name = "Acme", AccountCode = "C00001", StartDate = new DateOnly(2026, 8, 1), CreatedUtc = DateTimeOffset.UtcNow };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var project = new Project
        {
            ClientId = client.Id, Name = "ERP", Code = "ERP", PaymentModel = PaymentModel.FixedProjectCost,
            StartDate = new DateOnly(2026, 8, 1), IsActive = true, CreatedUtc = DateTimeOffset.UtcNow, CanInvoice = true,
            BudgetHours = 30m, FixedFeeAmount = 3000m,
        };
        var alice = new User { EntraObjectId = "oid-a", Email = "alice@svgit.co.uk", DisplayName = "Alice", PayrollNumber = "P0001", CreatedUtc = DateTimeOffset.UtcNow };
        var bob = new User { EntraObjectId = "oid-b", Email = "bob@svgit.co.uk", DisplayName = "Bob", PayrollNumber = "P0002", CreatedUtc = DateTimeOffset.UtcNow };
        db.Projects.Add(project);
        db.Users.AddRange(alice, bob);
        await db.SaveChangesAsync();

        foreach (var (isAlice, date, hours) in entries)
        {
            db.TimesheetEntries.Add(new TimesheetEntry
            {
                UserId = isAlice ? alice.Id : bob.Id, ClientId = client.Id, ProjectId = project.Id, Date = date,
                WorkHours = hours, Description = "Work", CreatedUtc = DateTimeOffset.UtcNow, ResolvedHourlyCost = 40m, ResolvedOutOfHoursCost = 0m,
            });
        }
        await db.SaveChangesAsync();
        return (project, alice, bob);
    }

    private static ReportingService CreateReporting(TimesheetDbContext db)
    {
        var projects = new ProjectRepository(db);
        var fx = new CurrencyConversionService(new CurrencyRateRepository(db), new NoopCurrencyRateProvider(), db);
        return new ReportingService(new ReportingRepository(db), new ClientRepository(db), projects, fx,
            new RevenueRecognitionService(projects, fx), new ProjectStatusService(new TimesheetEntryRepository(db)));
    }

    private static ReportDateRange Range(int fromDay, int toDay) =>
        new(new DateOnly(2026, 8, fromDay), new DateOnly(2026, 8, toDay), ReportRangePreset.Custom);

    [Fact]
    public async Task Breakdown_OverBudget_RecognizesTheFeeNotMore_SplitByHours()
    {
        await using var db = CreateInMemoryDb();
        // 40h logged against a 30h budget.
        var (project, alice, bob) = await SeedAsync(db, (true, new(2026, 8, 10), 30m), (false, new(2026, 8, 11), 10m));
        var projects = new ProjectRepository(db);
        var fx = new CurrencyConversionService(new CurrencyRateRepository(db), new NoopCurrencyRateProvider(), db);
        var service = new ProjectBreakdownService(new TimesheetEntryRepository(db), new ClientRepository(db), new RevenueRecognitionService(projects, fx));

        var breakdown = await service.GetBreakdownAsync(project, CancellationToken.None);

        Assert.Equal(3000m, breakdown.RecognizedRevenueToDate); // was 4000 uncapped
        Assert.Equal(2250m, breakdown.ByStaff.Single(l => l.UserId == alice.Id).Revenue); // 30 of 40 hours
        Assert.Equal(750m, breakdown.ByStaff.Single(l => l.UserId == bob.Id).Revenue);
    }

    [Fact]
    public async Task ProfitReport_RangeCrossesTheBudget_OnlyHoursUpToTheBudgetEarn()
    {
        await using var db = CreateInMemoryDb();
        // 25h before the range, then 10h inside it: only 5h of budget left, so 5h x £100.
        var (project, _, _) = await SeedAsync(db, (true, new(2026, 8, 5), 25m), (true, new(2026, 8, 15), 10m));

        var report = await CreateReporting(db).GetProfitOnProjectReportAsync(project.Id, Range(10, 31), CancellationToken.None);

        Assert.Equal(500m, report.Summary.Billed); // was 1000 uncapped
        Assert.Equal(400m, report.Summary.Cost);   // all 10h still cost money
        Assert.Equal(100m, report.Summary.Profit);
    }

    [Fact]
    public async Task ProfitReport_BudgetAlreadyUsedUp_RecognizesNothing_ButCostStillCounts()
    {
        await using var db = CreateInMemoryDb();
        var (project, _, _) = await SeedAsync(db, (true, new(2026, 8, 5), 30m), (false, new(2026, 8, 20), 8m));

        var report = await CreateReporting(db).GetProfitOnProjectByRoleReportAsync(project.Id, Range(10, 31), CancellationToken.None);

        Assert.Equal(0m, report.Summary.Billed);
        Assert.Equal(-320m, report.Summary.Profit); // 8h x £40 cost, no revenue
    }

    [Fact]
    public async Task ProfitReport_AllPeriodsTogether_NeverExceedTheFee()
    {
        await using var db = CreateInMemoryDb();
        var (project, _, _) = await SeedAsync(db,
            (true, new(2026, 8, 3), 12m), (false, new(2026, 8, 12), 12m), (true, new(2026, 8, 22), 12m)); // 36h total
        var reporting = CreateReporting(db);

        decimal total = 0;
        foreach (var (from, to) in new[] { (1, 10), (11, 20), (21, 31) })
        {
            total += (await reporting.GetProfitOnProjectReportAsync(project.Id, Range(from, to), CancellationToken.None)).Summary.Billed;
        }

        Assert.Equal(3000m, total); // 1200 + 1200 + 600 - the third period only has 6h of budget left
    }

    [Fact]
    public async Task ProfitOnClientReport_FixedFeeLine_IsCappedToo()
    {
        await using var db = CreateInMemoryDb();
        var (project, _, _) = await SeedAsync(db, (true, new(2026, 8, 5), 25m), (true, new(2026, 8, 15), 10m));

        var report = await CreateReporting(db).GetProfitOnClientReportAsync(project.ClientId, Range(10, 31), CancellationToken.None);

        Assert.Equal(500m, report.Breakdown.Single(l => l.ProjectId == project.Id).Billed);
    }
}
