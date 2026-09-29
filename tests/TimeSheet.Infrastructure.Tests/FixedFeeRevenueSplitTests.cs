using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Services;
using TimeSheet.Infrastructure.Data;
using TimeSheet.Infrastructure.Repositories;
using TimeSheet.Infrastructure.Services;
using Xunit;

namespace TimeSheet.Infrastructure.Tests;

/// <summary>Fixed Fee revenue split across per-row report/breakdown lines by hours (RevenueAllocation), replacing
/// the old Billed 0 / Profit -Cost per row that read as a loss. The rows must always sum to exactly the
/// project's recognized revenue.</summary>
public class FixedFeeRevenueSplitTests
{
    [Fact]
    public void ByHours_SplitsProportionally()
    {
        Assert.Equal([3000m, 1000m], RevenueAllocation.ByHours(4000m, [30m, 10m]));
    }

    [Fact]
    public void ByHours_AwkwardSplit_RemainderGoesToLargestRow_AndSumsExactly()
    {
        // 100 / 3 = 33.333... - rounds to 33.33 each (99.99), the missing penny goes to the row with most hours.
        var shares = RevenueAllocation.ByHours(100m, [1m, 2m, 1m]);
        Assert.Equal(100m, shares.Sum());
        Assert.Equal([25m, 50m, 25m], shares);

        var thirds = RevenueAllocation.ByHours(100m, [1m, 1m, 1m]);
        Assert.Equal(100m, thirds.Sum());
        Assert.Equal([33.34m, 33.33m, 33.33m], thirds);
    }

    [Fact]
    public void ByHours_NoHours_AllZero()
    {
        Assert.Equal([0m, 0m], RevenueAllocation.ByHours(500m, [0m, 0m]));
        Assert.Empty(RevenueAllocation.ByHours(500m, []));
    }

    private static TimesheetDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<TimesheetDbContext>().UseSqlite("Data Source=:memory:").Options;
        var db = new TimesheetDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    /// <summary>Fixed Fee £3,000 over 30 budget hours (£100 recognized per hour). Alice logs 20h, Bob 10h, so
    /// £3,000 recognized splits £2,000 / £1,000.</summary>
    private static async Task<(Project Project, User Alice, User Bob)> SeedFixedFeeAsync(TimesheetDbContext db)
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

        db.TimesheetEntries.AddRange(
            new TimesheetEntry
            {
                UserId = alice.Id, ClientId = client.Id, ProjectId = project.Id, Date = new DateOnly(2026, 8, 10),
                WorkHours = 20m, Description = "Build", CreatedUtc = DateTimeOffset.UtcNow, ResolvedHourlyCost = 40m, ResolvedOutOfHoursCost = 0m,
            },
            new TimesheetEntry
            {
                UserId = bob.Id, ClientId = client.Id, ProjectId = project.Id, Date = new DateOnly(2026, 8, 11),
                WorkHours = 10m, Description = "Test", CreatedUtc = DateTimeOffset.UtcNow, ResolvedHourlyCost = 30m, ResolvedOutOfHoursCost = 0m,
            });
        await db.SaveChangesAsync();
        return (project, alice, bob);
    }

    [Fact]
    public async Task ProfitOnProjectReport_FixedFee_RowsGetTheirHoursShare_AndSumToTheSummary()
    {
        await using var db = CreateInMemoryDb();
        var (project, alice, bob) = await SeedFixedFeeAsync(db);

        var projects = new ProjectRepository(db);
        var currencyConversion = new CurrencyConversionService(new CurrencyRateRepository(db), new NoopCurrencyRateProvider(), db);
        var reportingService = new ReportingService(new ReportingRepository(db), new ClientRepository(db), projects, currencyConversion,
            new RevenueRecognitionService(projects, currencyConversion), new ProjectStatusService(new TimesheetEntryRepository(db)));

        var range = new ReportDateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), ReportRangePreset.Custom);
        var report = await reportingService.GetProfitOnProjectReportAsync(project.Id, range, CancellationToken.None);

        Assert.Equal(3000m, report.Summary.Billed);
        Assert.Equal(report.Summary.Billed, report.Breakdown.Sum(l => l.Billed));

        var aliceLine = report.Breakdown.Single(l => l.UserId == alice.Id);
        Assert.Equal(2000m, aliceLine.Billed);
        Assert.Equal(1200m, aliceLine.Profit); // 2000 - 20h * £40

        var bobLine = report.Breakdown.Single(l => l.UserId == bob.Id);
        Assert.Equal(1000m, bobLine.Billed);
        Assert.Equal(700m, bobLine.Profit); // 1000 - 10h * £30
    }

    [Fact]
    public async Task ProjectBreakdown_FixedFee_StaffRevenueIsTheirHoursShare_AndSumsToRecognizedRevenue()
    {
        await using var db = CreateInMemoryDb();
        var (project, alice, bob) = await SeedFixedFeeAsync(db);

        var projects = new ProjectRepository(db);
        var currencyConversion = new CurrencyConversionService(new CurrencyRateRepository(db), new NoopCurrencyRateProvider(), db);
        var service = new ProjectBreakdownService(new TimesheetEntryRepository(db), new ClientRepository(db),
            new RevenueRecognitionService(projects, currencyConversion));

        var breakdown = await service.GetBreakdownAsync(project, CancellationToken.None);

        Assert.Equal(3000m, breakdown.RecognizedRevenueToDate);
        Assert.Equal(3000m, breakdown.ByStaff.Sum(l => l.Revenue));
        Assert.Equal(2000m, breakdown.ByStaff.Single(l => l.UserId == alice.Id).Revenue);
        Assert.Equal(1200m, breakdown.ByStaff.Single(l => l.UserId == alice.Id).Profit);
        Assert.Equal(1000m, breakdown.ByStaff.Single(l => l.UserId == bob.Id).Revenue);
    }

    /// <summary>Same-currency conversions never reach the provider - same stub as ReportingServiceTests.</summary>
    private class NoopCurrencyRateProvider : ICurrencyRateProvider
    {
        public Task<decimal?> FetchRateAsync(string baseCurrency, string quoteCurrency, DateOnly date, CancellationToken ct) =>
            throw new InvalidOperationException("Should not be called for a same-currency conversion.");
    }
}
