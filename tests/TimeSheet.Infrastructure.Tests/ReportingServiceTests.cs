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
            StartDate = new DateOnly(2026, 8, 1), IsActive = true, CreatedUtc = DateTimeOffset.UtcNow, CanInvoice = true,
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
        var projectStatus = new ProjectStatusService(new TimesheetEntryRepository(db));

        var reportingService = new ReportingService(reportingRepo, clients, projects, currencyConversion, revenueRecognition, projectStatus);

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

    /// <summary>A project InvoiceGenerationService will never actually invoice (CanInvoice not true) must not
    /// show as revenue/profit in reports either - otherwise a project like "Training" (not billed, not costed)
    /// would still inflate a client's reported revenue and profit. Cost is deliberately unaffected here - a
    /// non-invoiceable project can still have a real cost impact unless it's also Project.IsCostExempt.</summary>
    [Fact]
    public async Task GetProfitOnProjectReportAsync_ProjectNotInvoiceable_BilledIsZeroButCostIsNot()
    {
        await using var db = CreateInMemoryDb();

        var client = new Client { Name = "Antigua", AccountCode = "C13673", StartDate = new DateOnly(2026, 8, 1), CreatedUtc = DateTimeOffset.UtcNow };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var project = new Project
        {
            ClientId = client.Id, Name = "Training", Code = "TRAINING", PaymentModel = PaymentModel.TimeAndMaterials,
            StartDate = new DateOnly(2026, 8, 1), IsActive = true, CreatedUtc = DateTimeOffset.UtcNow, CanInvoice = false,
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

        db.TimesheetEntries.Add(new TimesheetEntry
        {
            UserId = user.Id, ClientId = client.Id, ProjectId = project.Id, Date = new DateOnly(2026, 8, 10),
            WorkHours = 10m, OutOfHoursHours = 0m, Description = "Training", CreatedUtc = DateTimeOffset.UtcNow,
            ResolvedCustomerRate = 100m, ResolvedHourlyCost = 40m, ResolvedOutOfHoursCost = 0m,
        });
        await db.SaveChangesAsync();

        var clients = new ClientRepository(db);
        var projects = new ProjectRepository(db);
        var reportingRepo = new ReportingRepository(db);
        var currencyConversion = new CurrencyConversionService(new CurrencyRateRepository(db), new NoopCurrencyRateProvider(), db);
        var revenueRecognition = new RevenueRecognitionService(projects, currencyConversion);
        var projectStatus = new ProjectStatusService(new TimesheetEntryRepository(db));
        var reportingService = new ReportingService(reportingRepo, clients, projects, currencyConversion, revenueRecognition, projectStatus);

        var range = new ReportDateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), ReportRangePreset.Custom);
        var report = await reportingService.GetProfitOnProjectReportAsync(project.Id, range, CancellationToken.None);

        Assert.Equal(0m, report.Summary.Billed);
        Assert.Equal(400m, report.Summary.Cost); // 10h * £40 - cost still applies, this project isn't cost-exempt
        Assert.Equal(-400m, report.Summary.Profit);
    }

    /// <summary>Same rule as above, for the Fixed Project Cost revenue-recognition path (RevenueRecognitionService)
    /// rather than the Time & Materials BilledAmountNative sum - a non-invoiceable Fixed Fee project must not
    /// recognize revenue off its budget/fee either.</summary>
    [Fact]
    public async Task GetProfitOnProjectReportAsync_FixedFeeProjectNotInvoiceable_RecognizesNoRevenue()
    {
        await using var db = CreateInMemoryDb();

        var client = new Client { Name = "Antigua", AccountCode = "C13673", StartDate = new DateOnly(2026, 8, 1), CreatedUtc = DateTimeOffset.UtcNow };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var project = new Project
        {
            ClientId = client.Id, Name = "Fixed Retainer", Code = "FIXED-01", PaymentModel = PaymentModel.FixedProjectCost,
            StartDate = new DateOnly(2026, 8, 1), IsActive = true, CreatedUtc = DateTimeOffset.UtcNow, CanInvoice = false,
            BudgetHours = 20m, FixedFeeAmount = 2000m,
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

        db.TimesheetEntries.Add(new TimesheetEntry
        {
            UserId = user.Id, ClientId = client.Id, ProjectId = project.Id, Date = new DateOnly(2026, 8, 10),
            WorkHours = 10m, OutOfHoursHours = 0m, Description = "Retainer work", CreatedUtc = DateTimeOffset.UtcNow,
            ResolvedHourlyCost = 40m, ResolvedOutOfHoursCost = 0m,
        });
        await db.SaveChangesAsync();

        var clients = new ClientRepository(db);
        var projects = new ProjectRepository(db);
        var reportingRepo = new ReportingRepository(db);
        var currencyConversion = new CurrencyConversionService(new CurrencyRateRepository(db), new NoopCurrencyRateProvider(), db);
        var revenueRecognition = new RevenueRecognitionService(projects, currencyConversion);
        var projectStatus = new ProjectStatusService(new TimesheetEntryRepository(db));
        var reportingService = new ReportingService(reportingRepo, clients, projects, currencyConversion, revenueRecognition, projectStatus);

        var range = new ReportDateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), ReportRangePreset.Custom);
        var report = await reportingService.GetProfitOnProjectReportAsync(project.Id, range, CancellationToken.None);

        Assert.Equal(0m, report.Summary.Billed);
        Assert.Equal(400m, report.Summary.Cost);
        Assert.Equal(-400m, report.Summary.Profit);
    }

    [Fact]
    public async Task GetProfitOnProjectReportAsync_MultipleEntriesSameUserAndDate_EntryCountReflectsRawEntryRows()
    {
        await using var db = CreateInMemoryDb();

        var client = new Client { Name = "Antigua", AccountCode = "C13673", StartDate = new DateOnly(2026, 8, 1), CreatedUtc = DateTimeOffset.UtcNow };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var project = new Project
        {
            ClientId = client.Id, Name = "FDD-3145", Code = "FDD-3145", PaymentModel = PaymentModel.TimeAndMaterials,
            StartDate = new DateOnly(2026, 8, 1), IsActive = true, CreatedUtc = DateTimeOffset.UtcNow, CanInvoice = true,
        };
        db.Projects.Add(project);
        var user = new User
        {
            EntraObjectId = "oid-1", Email = "mark@svgit.co.uk", DisplayName = "Mark Llewellyn",
            PayrollNumber = "P0001", Role = UserRole.User, CreatedUtc = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Two separate TimesheetEntry rows for the same user/project/date - the repository's existing
        // aggregation groups these into a single (Project,Client,User,Date) row (summing hours), so EntryCount
        // is the only way to tell "2 entries totalling 10h" apart from "1 entry of 10h" - proves it counts raw
        // rows, not days or hour-sums.
        db.TimesheetEntries.Add(new TimesheetEntry
        {
            UserId = user.Id, ClientId = client.Id, ProjectId = project.Id, Date = new DateOnly(2026, 8, 10),
            WorkHours = 6m, OutOfHoursHours = 0m, Description = "Morning", CreatedUtc = DateTimeOffset.UtcNow,
            ResolvedCustomerRate = 100m, ResolvedHourlyCost = 40m, ResolvedOutOfHoursCost = 0m,
        });
        db.TimesheetEntries.Add(new TimesheetEntry
        {
            UserId = user.Id, ClientId = client.Id, ProjectId = project.Id, Date = new DateOnly(2026, 8, 10),
            WorkHours = 4m, OutOfHoursHours = 0m, Description = "Afternoon", CreatedUtc = DateTimeOffset.UtcNow,
            ResolvedCustomerRate = 100m, ResolvedHourlyCost = 40m, ResolvedOutOfHoursCost = 0m,
        });
        await db.SaveChangesAsync();

        var clients = new ClientRepository(db);
        var projects = new ProjectRepository(db);
        var reportingRepo = new ReportingRepository(db);
        var currencyConversion = new CurrencyConversionService(new CurrencyRateRepository(db), new NoopCurrencyRateProvider(), db);
        var revenueRecognition = new RevenueRecognitionService(projects, currencyConversion);
        var projectStatus = new ProjectStatusService(new TimesheetEntryRepository(db));
        var reportingService = new ReportingService(reportingRepo, clients, projects, currencyConversion, revenueRecognition, projectStatus);

        var range = new ReportDateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), ReportRangePreset.Custom);
        var report = await reportingService.GetProfitOnProjectReportAsync(project.Id, range, CancellationToken.None);

        Assert.Equal(1000m, report.Summary.Billed); // 10h combined * £100
        Assert.Equal(2, report.Summary.EntryCount);
        Assert.Single(report.Breakdown);
        Assert.Equal(2, report.Breakdown[0].EntryCount);
    }

    [Fact]
    public async Task GetTimeOnProjectByRoleReportAsync_GroupsByCurrentRole_UnassignedBucketForNoJobRole()
    {
        await using var db = CreateInMemoryDb();

        var client = new Client { Name = "Antigua", AccountCode = "C13673", StartDate = new DateOnly(2026, 8, 1), CreatedUtc = DateTimeOffset.UtcNow };
        db.Clients.Add(client);
        var role = new Role { Name = "Consultant", CreatedUtc = DateTimeOffset.UtcNow };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var project = new Project
        {
            ClientId = client.Id, Name = "FDD-3145", Code = "FDD-3145", PaymentModel = PaymentModel.TimeAndMaterials,
            StartDate = new DateOnly(2026, 8, 1), IsActive = true, CreatedUtc = DateTimeOffset.UtcNow, CanInvoice = true,
        };
        db.Projects.Add(project);

        var withRole = new User
        {
            EntraObjectId = "oid-1", Email = "with-role@svgit.co.uk", DisplayName = "Has Role",
            PayrollNumber = "P0001", JobRoleId = role.Id, CreatedUtc = DateTimeOffset.UtcNow,
        };
        var withoutRole = new User
        {
            EntraObjectId = "oid-2", Email = "no-role@svgit.co.uk", DisplayName = "No Role",
            PayrollNumber = "P0002", CreatedUtc = DateTimeOffset.UtcNow,
        };
        db.Users.AddRange(withRole, withoutRole);
        await db.SaveChangesAsync();

        db.TimesheetEntries.Add(new TimesheetEntry
        {
            UserId = withRole.Id, ClientId = client.Id, ProjectId = project.Id, Date = new DateOnly(2026, 8, 10),
            WorkHours = 5m, OutOfHoursHours = 0m, Description = "Consultant work", CreatedUtc = DateTimeOffset.UtcNow,
        });
        db.TimesheetEntries.Add(new TimesheetEntry
        {
            UserId = withoutRole.Id, ClientId = client.Id, ProjectId = project.Id, Date = new DateOnly(2026, 8, 11),
            WorkHours = 3m, OutOfHoursHours = 0m, Description = "No role work", CreatedUtc = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var clients = new ClientRepository(db);
        var projects = new ProjectRepository(db);
        var reportingRepo = new ReportingRepository(db);
        var currencyConversion = new CurrencyConversionService(new CurrencyRateRepository(db), new NoopCurrencyRateProvider(), db);
        var revenueRecognition = new RevenueRecognitionService(projects, currencyConversion);
        var projectStatus = new ProjectStatusService(new TimesheetEntryRepository(db));
        var reportingService = new ReportingService(reportingRepo, clients, projects, currencyConversion, revenueRecognition, projectStatus);

        var range = new ReportDateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), ReportRangePreset.Custom);
        var report = await reportingService.GetTimeOnProjectByRoleReportAsync(project.Id, range, CancellationToken.None);

        Assert.Equal(2, report.Breakdown.Count);
        var consultantLine = Assert.Single(report.Breakdown, l => l.RoleId == role.Id);
        Assert.Equal("Consultant", consultantLine.RoleName);
        Assert.Equal(5m, consultantLine.TotalHours);
        Assert.Equal(1, consultantLine.EntryCount);

        var unassignedLine = Assert.Single(report.Breakdown, l => l.RoleId == null);
        Assert.Equal("Unassigned", unassignedLine.RoleName);
        Assert.Equal(3m, unassignedLine.TotalHours);
        Assert.Equal(1, unassignedLine.EntryCount);

        Assert.Equal(8m, report.Summary.TotalHours);
        Assert.Equal(2, report.Summary.EntryCount);
    }

    /// <summary>FDD's "hours remaining" ask. Deliberately seeds one entry inside the report's own date range and
    /// one entry well outside it, on the same project, to prove HoursRemaining reflects the project's real
    /// ALL-TIME actual hours (matching IProjectStatusService's own convention) rather than only what the
    /// selected date range happens to cover - the report's own TotalHours (5h, in-range only) and the budget
    /// figure's ActualHours baked into HoursRemaining (8h, all-time) are deliberately different numbers here.</summary>
    [Fact]
    public async Task GetTimeOnProjectReportAsync_ProjectHasBudgetHours_HoursRemainingReflectsAllTimeActuals()
    {
        await using var db = CreateInMemoryDb();

        var client = new Client { Name = "Antigua", AccountCode = "C13673", StartDate = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var project = new Project
        {
            ClientId = client.Id, Name = "FDD-3145", Code = "FDD-3145", PaymentModel = PaymentModel.TimeAndMaterials,
            StartDate = new DateOnly(2026, 1, 1), IsActive = true, CreatedUtc = DateTimeOffset.UtcNow, CanInvoice = true,
            BudgetHours = 20m,
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

        // In the report's own August date range.
        db.TimesheetEntries.Add(new TimesheetEntry
        {
            UserId = user.Id, ClientId = client.Id, ProjectId = project.Id, Date = new DateOnly(2026, 8, 10),
            WorkHours = 5m, OutOfHoursHours = 0m, Description = "In range", CreatedUtc = DateTimeOffset.UtcNow,
        });
        // Well outside it (January) - still counts toward the project's all-time actual hours.
        db.TimesheetEntries.Add(new TimesheetEntry
        {
            UserId = user.Id, ClientId = client.Id, ProjectId = project.Id, Date = new DateOnly(2026, 1, 15),
            WorkHours = 3m, OutOfHoursHours = 0m, Description = "Out of range", CreatedUtc = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var clients = new ClientRepository(db);
        var projects = new ProjectRepository(db);
        var reportingRepo = new ReportingRepository(db);
        var currencyConversion = new CurrencyConversionService(new CurrencyRateRepository(db), new NoopCurrencyRateProvider(), db);
        var revenueRecognition = new RevenueRecognitionService(projects, currencyConversion);
        var projectStatus = new ProjectStatusService(new TimesheetEntryRepository(db));
        var reportingService = new ReportingService(reportingRepo, clients, projects, currencyConversion, revenueRecognition, projectStatus);

        var range = new ReportDateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), ReportRangePreset.Custom);
        var report = await reportingService.GetTimeOnProjectReportAsync(project.Id, range, CancellationToken.None);

        Assert.Equal(5m, report.Summary.TotalHours); // only the in-range entry
        Assert.Equal(20m, report.Summary.BudgetHours);
        Assert.Equal(12m, report.Summary.HoursRemaining); // 20 - (5 + 3) all-time, not 20 - 5
    }

    [Fact]
    public async Task GetTimeOnProjectReportAsync_ProjectHasNoBudgetHours_HoursRemainingIsNullNotZero()
    {
        await using var db = CreateInMemoryDb();

        var client = new Client { Name = "Antigua", AccountCode = "C13673", StartDate = new DateOnly(2026, 8, 1), CreatedUtc = DateTimeOffset.UtcNow };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var project = new Project
        {
            ClientId = client.Id, Name = "FDD-3145", Code = "FDD-3145", PaymentModel = PaymentModel.TimeAndMaterials,
            StartDate = new DateOnly(2026, 8, 1), IsActive = true, CreatedUtc = DateTimeOffset.UtcNow, CanInvoice = true,
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var clients = new ClientRepository(db);
        var projects = new ProjectRepository(db);
        var reportingRepo = new ReportingRepository(db);
        var currencyConversion = new CurrencyConversionService(new CurrencyRateRepository(db), new NoopCurrencyRateProvider(), db);
        var revenueRecognition = new RevenueRecognitionService(projects, currencyConversion);
        var projectStatus = new ProjectStatusService(new TimesheetEntryRepository(db));
        var reportingService = new ReportingService(reportingRepo, clients, projects, currencyConversion, revenueRecognition, projectStatus);

        var range = new ReportDateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), ReportRangePreset.Custom);
        var report = await reportingService.GetTimeOnProjectReportAsync(project.Id, range, CancellationToken.None);

        Assert.Null(report.Summary.BudgetHours);
        Assert.Null(report.Summary.HoursRemaining);
    }

    /// <summary>The FDD's "hours remaining" ask on a client-wide report: each project's own line carries its own
    /// truth (one has a budget, one doesn't), but the client-level summary deliberately leaves both fields null
    /// rather than publish a single aggregate across projects with different (or absent) budgets - see
    /// TimeByProjectLine's own doc comment.</summary>
    [Fact]
    public async Task GetTimeOnClientReportAsync_PerProjectLinesCarryHoursRemaining_ButClientSummaryDoesNot()
    {
        await using var db = CreateInMemoryDb();

        var client = new Client { Name = "Antigua", AccountCode = "C13673", StartDate = new DateOnly(2026, 8, 1), CreatedUtc = DateTimeOffset.UtcNow };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var budgeted = new Project
        {
            ClientId = client.Id, Name = "Budgeted", Code = "BUDGETED", PaymentModel = PaymentModel.TimeAndMaterials,
            StartDate = new DateOnly(2026, 8, 1), IsActive = true, CreatedUtc = DateTimeOffset.UtcNow, CanInvoice = true,
            BudgetHours = 20m,
        };
        var unbudgeted = new Project
        {
            ClientId = client.Id, Name = "Unbudgeted", Code = "UNBUDGETED", PaymentModel = PaymentModel.TimeAndMaterials,
            StartDate = new DateOnly(2026, 8, 1), IsActive = true, CreatedUtc = DateTimeOffset.UtcNow, CanInvoice = true,
        };
        db.Projects.AddRange(budgeted, unbudgeted);
        await db.SaveChangesAsync();

        var user = new User
        {
            EntraObjectId = "oid-1", Email = "mark@svgit.co.uk", DisplayName = "Mark Llewellyn",
            PayrollNumber = "P0001", Role = UserRole.User, CreatedUtc = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.TimesheetEntries.Add(new TimesheetEntry
        {
            UserId = user.Id, ClientId = client.Id, ProjectId = budgeted.Id, Date = new DateOnly(2026, 8, 10),
            WorkHours = 5m, OutOfHoursHours = 0m, Description = "Budgeted work", CreatedUtc = DateTimeOffset.UtcNow,
        });
        db.TimesheetEntries.Add(new TimesheetEntry
        {
            UserId = user.Id, ClientId = client.Id, ProjectId = unbudgeted.Id, Date = new DateOnly(2026, 8, 11),
            WorkHours = 2m, OutOfHoursHours = 0m, Description = "Unbudgeted work", CreatedUtc = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var clients = new ClientRepository(db);
        var projects = new ProjectRepository(db);
        var reportingRepo = new ReportingRepository(db);
        var currencyConversion = new CurrencyConversionService(new CurrencyRateRepository(db), new NoopCurrencyRateProvider(), db);
        var revenueRecognition = new RevenueRecognitionService(projects, currencyConversion);
        var projectStatus = new ProjectStatusService(new TimesheetEntryRepository(db));
        var reportingService = new ReportingService(reportingRepo, clients, projects, currencyConversion, revenueRecognition, projectStatus);

        var range = new ReportDateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), ReportRangePreset.Custom);
        var report = await reportingService.GetTimeOnClientReportAsync(client.Id, range, CancellationToken.None);

        var budgetedLine = Assert.Single(report.Breakdown, l => l.ProjectId == budgeted.Id);
        Assert.Equal(20m, budgetedLine.BudgetHours);
        Assert.Equal(15m, budgetedLine.HoursRemaining);

        var unbudgetedLine = Assert.Single(report.Breakdown, l => l.ProjectId == unbudgeted.Id);
        Assert.Null(unbudgetedLine.BudgetHours);
        Assert.Null(unbudgetedLine.HoursRemaining);

        Assert.Null(report.Summary.BudgetHours);
        Assert.Null(report.Summary.HoursRemaining);
    }

    /// <summary>Never called in this test - same-currency conversions short-circuit before reaching the
    /// provider - but ICurrencyRateProvider has no parameterless implementation to new up otherwise.</summary>
    private class NoopCurrencyRateProvider : Domain.Services.ICurrencyRateProvider
    {
        public Task<decimal?> FetchRateAsync(string baseCurrency, string quoteCurrency, DateOnly date, CancellationToken ct) =>
            throw new InvalidOperationException("Should not be called for a same-currency conversion.");
    }
}
