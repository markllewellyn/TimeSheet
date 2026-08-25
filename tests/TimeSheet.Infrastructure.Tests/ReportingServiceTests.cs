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

        var client = new Client { Name = "Antigua", AccountCode = "C13673", ReportingCurrencyCode = "GBP", CreatedUtc = DateTimeOffset.UtcNow };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var project = new Project
        {
            ClientId = client.Id, Name = "FDD-3145", Code = "FDD-3145", PaymentModel = PaymentModel.TimeAndMaterials,
            StartDate = new DateOnly(2026, 8, 1), BudgetAlertThresholdPercent = 80, IsActive = true, CreatedUtc = DateTimeOffset.UtcNow,
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var user = new User { EntraObjectId = "oid-1", Email = "mark@svgit.co.uk", DisplayName = "Mark Llewellyn", Role = UserRole.User, CreatedUtc = DateTimeOffset.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.ProjectRates.Add(new ProjectRate
        {
            ProjectId = project.Id, UserId = null, BillingRatePerHour = 100m, CostRatePerHour = 40m,
            EffectiveFrom = new DateOnly(2026, 8, 1), CreatedUtc = DateTimeOffset.UtcNow,
        });

        db.TimesheetEntries.Add(new TimesheetEntry
        {
            UserId = user.Id, ProjectId = project.Id, Date = new DateOnly(2026, 8, 10),
            WorkHours = 10m, OutOfHoursHours = 0m, Status = TimesheetEntryStatus.Normal, CreatedUtc = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var clients = new ClientRepository(db);
        var projects = new ProjectRepository(db);
        var rates = new ProjectRateRepository(db);
        var reportingRepo = new ReportingRepository(db);
        var rateResolver = new RateResolver(rates);
        var currencyConversion = new CurrencyConversionService(new CurrencyRateRepository(db), new NoopCurrencyRateProvider(), db);
        var revenueRecognition = new RevenueRecognitionService(projects, currencyConversion);

        var reportingService = new ReportingService(reportingRepo, clients, projects, rateResolver, currencyConversion, revenueRecognition);

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
