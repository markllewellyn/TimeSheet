using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Services;
using TimeSheet.Infrastructure.Data;
using TimeSheet.Infrastructure.Repositories;
using TimeSheet.Infrastructure.Services;
using Xunit;

namespace TimeSheet.Infrastructure.Tests;

/// <summary>Covers TimesheetEntry.BillingPeriodChoice's effect on BuildDraftAsync (FDD: "the user can choose to
/// add it to the current billing period or to the next billing period"), exercising
/// ITimesheetEntryRepository.GetCountedForInvoicingAsync's period-boundary logic end to end. Every client here
/// is GBP-only with no CurrencyOverride, so ConvertAsync's Frankfurter path is never exercised -
/// ThrowingRateProvider proves that.</summary>
public class InvoiceGenerationServiceTests
{
    private static readonly DateOnly PeriodStart = new(2026, 2, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 2, 28);

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
        Client client, Project project, User user, DateOnly date, decimal workHours, decimal resolvedCustomerRate,
        BillingPeriodChoice choice = BillingPeriodChoice.Current) => new()
    {
        UserId = user.Id, ClientId = client.Id, ProjectId = project.Id,
        Date = date, WorkHours = workHours, OutOfHoursHours = 0, Description = "Work",
        ResolvedCustomerRate = resolvedCustomerRate, BillingPeriodChoice = choice,
        CreatedUtc = DateTimeOffset.UtcNow,
    };

    private static InvoiceGenerationService CreateService(TimesheetDbContext db) => new(
        new ClientRepository(db), new ProjectRepository(db), new TimesheetEntryRepository(db),
        new ExpenseEntryRepository(db), new CurrencyConversionService(new CurrencyRateRepository(db), new ThrowingRateProvider(), db));

    [Fact]
    public async Task BuildDraftAsync_EntryChoiceCurrentInPeriod_Counted()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedAsync(db);
        db.TimesheetEntries.Add(MakeEntry(client, project, user, new DateOnly(2026, 2, 15), 4m, 100m));
        await db.SaveChangesAsync();

        var invoice = await CreateService(db).BuildDraftAsync(client.Id, PeriodStart, PeriodEnd, null, CancellationToken.None);

        var line = Assert.Single(invoice.LineItems);
        Assert.Equal(4m, line.Hours);
        Assert.Equal(400m, line.Amount);
    }

    [Fact]
    public async Task BuildDraftAsync_EntryChoiceNextInPeriod_ExcludedFromItsOwnPeriod()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedAsync(db);
        // Dated inside Feb (the period being generated) but deferred - should NOT appear on the Feb invoice.
        db.TimesheetEntries.Add(MakeEntry(client, project, user, new DateOnly(2026, 2, 27), 4m, 100m, BillingPeriodChoice.Next));
        await db.SaveChangesAsync();

        var invoice = await CreateService(db).BuildDraftAsync(client.Id, PeriodStart, PeriodEnd, null, CancellationToken.None);

        Assert.Empty(invoice.LineItems);
    }

    [Fact]
    public async Task BuildDraftAsync_EntryDeferredFromPreviousPeriod_CountedInThisOne()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedAsync(db);
        // Dated in January (the period immediately before Feb) and deferred - should land on the Feb invoice.
        db.TimesheetEntries.Add(MakeEntry(client, project, user, new DateOnly(2026, 1, 20), 3m, 100m, BillingPeriodChoice.Next));
        await db.SaveChangesAsync();

        var invoice = await CreateService(db).BuildDraftAsync(client.Id, PeriodStart, PeriodEnd, null, CancellationToken.None);

        var line = Assert.Single(invoice.LineItems);
        Assert.Equal(3m, line.Hours);
        Assert.Equal(300m, line.Amount);
    }

    [Fact]
    public async Task BuildDraftAsync_EntryDeferredTwoPeriodsBack_NotSweptUpLater()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedAsync(db);
        // Dated in December (two periods before Feb) and deferred - "next" means the immediately following
        // period only, not "whenever a draft next happens to run" - so this must NOT appear on the Feb invoice.
        db.TimesheetEntries.Add(MakeEntry(client, project, user, new DateOnly(2025, 12, 20), 5m, 100m, BillingPeriodChoice.Next));
        await db.SaveChangesAsync();

        var invoice = await CreateService(db).BuildDraftAsync(client.Id, PeriodStart, PeriodEnd, null, CancellationToken.None);

        Assert.Empty(invoice.LineItems);
    }

    [Fact]
    public async Task BuildDraftAsync_PreviousPeriodEntryChoiceCurrent_NotPulledForward()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedAsync(db);
        // Dated in January with the default Current choice - belongs to January's own invoice, not Feb's.
        db.TimesheetEntries.Add(MakeEntry(client, project, user, new DateOnly(2026, 1, 20), 3m, 100m, BillingPeriodChoice.Current));
        await db.SaveChangesAsync();

        var invoice = await CreateService(db).BuildDraftAsync(client.Id, PeriodStart, PeriodEnd, null, CancellationToken.None);

        Assert.Empty(invoice.LineItems);
    }

    private class ThrowingRateProvider : ICurrencyRateProvider
    {
        public Task<decimal?> FetchRateAsync(string baseCurrency, string quoteCurrency, DateOnly date, CancellationToken ct) =>
            throw new InvalidOperationException("Not expected to be called for a GBP-only client.");
    }
}
