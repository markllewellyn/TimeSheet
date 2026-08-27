using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Services;
using TimeSheet.Infrastructure.Data;
using TimeSheet.Infrastructure.Repositories;
using TimeSheet.Infrastructure.Services;
using Xunit;

namespace TimeSheet.Infrastructure.Tests;

public class ReportingServiceTests
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

    [Fact]
    public async Task GetProfitOnProjectReportAsync_ComputesBilledCostAndProfit_ForTimeAndMaterials()
    {
        await using var db = CreateInMemoryDb();

        // No Currency row - Client.ReportingCurrencyCode falls back to "GBP", matching this test's expectations.
        var client = new Client { Name = "Antigua", AccountCode = "C13673", StartDate = new DateOnly(2026, 8, 1), CreatedUtc = DateTimeOffset.UtcNow };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var project = new Project
        {
            ClientId = client.Id, Name = "FDD-3145", Code = "FDD-3145", PaymentModel = PaymentModel.TimeAndMaterials,
            StartDate = new DateOnly(2026, 8, 1), BudgetAlertThresholdPercent = 80, IsActive = true, CreatedUtc = DateTimeOffset.UtcNow,
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var user = new User
        {
            EntraObjectId = "oid-1", Email = "mark@svgit.co.uk", DisplayName = "Mark Llewellyn",
            PayrollNumber = "P0001", Role = UserRole.User, CreatedUtc = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // ReportingService now sums each entry's own stamped Resolved* snapshot rather than re-resolving via
        // IRateResolver - so this test stamps the entry directly (£100/h billed, £40/h internal cost) instead
        // of seeding RateCard/StaffCost rows and relying on the resolver. RateResolver's own resolution logic
        // is covered separately by RateResolverTests.
        db.TimesheetEntries.Add(new TimesheetEntry
        {
            UserId = user.Id, ClientId = client.Id, ProjectId = project.Id, Date = new DateOnly(2026, 8, 10),
            WorkHours = 10m, OutOfHoursHours = 0m, Description = "FDD work", CreatedUtc = DateTimeOffset.UtcNow,
            ResolvedCustomerRate = 100m, ResolvedHourlyCost = 40m, ResolvedOutOfHoursCost = 0m,
        });
        await db.SaveChangesAsync();

        var clients = new ClientRepository(db);
        var projects = new ProjectRepository(db);
        var reportingRepo = new ReportingRepository(db);
        var currencyConversion = new CurrencyConversionService(new CurrencyRateRepository(db), new NoopCurrencyRateProvider(), db);
        var revenueRecognition = new RevenueRecognitionService(projects, currencyConversion);

        var reportingService = new ReportingService(reportingRepo, clients, projects, currencyConversion, revenueRecognition);

        var range = new ReportDateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), ReportRangePreset.Custom);
        var report = await reportingService.GetProfitOnProjectReportAsync(project.Id, range, CancellationToken.None);

        Assert.Equal("GBP", report.Currency);
        Assert.Equal(1000m, report.Summary.Billed); // 10h * £100
        Assert.Equal(400m, report.Summary.Cost);    // 10h * £40
        Assert.Equal(600m, report.Summary.Profit);
        Assert.Single(report.Breakdown);
        Assert.Equal(user.Id, report.Breakdown[0].UserId);
        Assert.Equal(600m, report.Breakdown[0].Profit);
    }

    /// <summary>Never called in this test - same-currency conversions short-circuit before reaching the
    /// provider - but ICurrencyRateProvider has no parameterless implementation to new up otherwise.</summary>
    private class NoopCurrencyRateProvider : Domain.Services.ICurrencyRateProvider
    {
        public Task<decimal?> FetchRateAsync(string baseCurrency, string quoteCurrency, DateOnly date, CancellationToken ct) =>
            throw new InvalidOperationException("Should not be called for a same-currency conversion.");
    }
}
