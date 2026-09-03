using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Services;
using TimeSheet.Infrastructure.Data;
using TimeSheet.Infrastructure.Repositories;
using TimeSheet.Infrastructure.Services;
using Xunit;

namespace TimeSheet.Infrastructure.Tests;

/// <summary>Covers InvoicingService.FinalizeInvoiceAsync's entry-locking behavior (FDD: "Finalizing an invoice
/// locks the entries it was built from"). BuildDraftAsync only persists an aggregated per-project sum on each
/// Time &amp; Materials line item - the individual TimesheetEntry rows behind it are otherwise never recorded -
/// so FinalizeInvoiceAsync has to reconstitute that entry set by re-running the same period/project query
/// (ITimesheetEntryRepository.GetCountedForInvoicingAsync) before stamping InvoiceId. Exercised end to end
/// against a real database, not mocked.</summary>
public class InvoicingServiceTests
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

    private static async Task<(Client Client, Project Project, User User)> SeedAsync(TimesheetDbContext db, PaymentModel paymentModel = PaymentModel.TimeAndMaterials)
    {
        var client = new Client { Name = "Antigua", AccountCode = "C1", StartDate = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow };
        db.Clients.Add(client);
        var user = new User { Email = "staff@svgit.co.uk", DisplayName = "Staff", PayrollNumber = "P0001", CreatedUtc = DateTimeOffset.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var project = new Project
        {
            ClientId = client.Id, Name = "Project A", Code = "A", PaymentModel = paymentModel,
            FixedFeeAmount = paymentModel == PaymentModel.FixedProjectCost ? 1000m : null,
            StartDate = new DateOnly(2026, 1, 1), IsActive = true, CreatedUtc = DateTimeOffset.UtcNow,
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return (client, project, user);
    }

    private static TimesheetEntry MakeEntry(
        Client client, Project project, User user, DateOnly date, decimal workHours, decimal resolvedCustomerRate) => new()
    {
        UserId = user.Id, ClientId = client.Id, ProjectId = project.Id,
        Date = date, WorkHours = workHours, OutOfHoursHours = 0, Description = "Work",
        ResolvedCustomerRate = resolvedCustomerRate, CreatedUtc = DateTimeOffset.UtcNow,
    };

    private static InvoicingService CreateService(TimesheetDbContext db) => new(
        new InvoiceRepository(db), new ClientRepository(db), new TimesheetEntryRepository(db),
        new InvoiceGenerationService(
            new ClientRepository(db), new ProjectRepository(db), new TimesheetEntryRepository(db),
            new ExpenseEntryRepository(db), new CurrencyConversionService(new CurrencyRateRepository(db), new ThrowingRateProvider(), db)),
        new StubPdfRenderer(), new RecordingNotificationService(), db);

    [Fact]
    public async Task FinalizeInvoiceAsync_TimeAndMaterialsLine_LocksTheEntriesItWasBuiltFrom()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedAsync(db);
        var counted = MakeEntry(client, project, user, new DateOnly(2026, 2, 15), 4m, 100m);
        db.TimesheetEntries.Add(counted);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var draft = await service.GenerateDraftInvoiceAsync(client.Id, PeriodStart, PeriodEnd, null, CancellationToken.None);
        var finalized = await service.FinalizeInvoiceAsync(draft.Id, "INV-0001", user.Id, CancellationToken.None);

        var reloaded = await db.TimesheetEntries.AsNoTracking().SingleAsync(e => e.Id == counted.Id);
        Assert.Equal(finalized.Id, reloaded.InvoiceId);
    }

    [Fact]
    public async Task FinalizeInvoiceAsync_EntryOutsidePeriod_NotLocked()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedAsync(db);
        var counted = MakeEntry(client, project, user, new DateOnly(2026, 2, 15), 4m, 100m);
        var uncounted = MakeEntry(client, project, user, new DateOnly(2026, 3, 5), 2m, 100m);
        db.TimesheetEntries.AddRange(counted, uncounted);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var draft = await service.GenerateDraftInvoiceAsync(client.Id, PeriodStart, PeriodEnd, null, CancellationToken.None);
        await service.FinalizeInvoiceAsync(draft.Id, "INV-0002", user.Id, CancellationToken.None);

        var reloaded = await db.TimesheetEntries.AsNoTracking().SingleAsync(e => e.Id == uncounted.Id);
        Assert.Null(reloaded.InvoiceId);
    }

    [Fact]
    public async Task FinalizeInvoiceAsync_FixedFeeProject_NoEntriesTouched()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedAsync(db, PaymentModel.FixedProjectCost);
        var entry = MakeEntry(client, project, user, new DateOnly(2026, 2, 15), 4m, 100m);
        db.TimesheetEntries.Add(entry);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var draft = await service.GenerateDraftInvoiceAsync(client.Id, PeriodStart, PeriodEnd, null, CancellationToken.None);
        await service.FinalizeInvoiceAsync(draft.Id, "INV-0003", user.Id, CancellationToken.None);

        var reloaded = await db.TimesheetEntries.AsNoTracking().SingleAsync(e => e.Id == entry.Id);
        Assert.Null(reloaded.InvoiceId);
    }

    private class RecordingNotificationService : INotificationService
    {
        public Task RaiseAsync(int recipientUserId, NotificationType type, string message, NotificationChannel channel = NotificationChannel.Both,
            int? relatedProjectId = null, int? relatedTimesheetEntryId = null, CancellationToken ct = default) => Task.CompletedTask;

        public Task RaiseToAdminsAsync(NotificationType type, string message, NotificationChannel channel = NotificationChannel.Both,
            int? relatedProjectId = null, int? relatedTimesheetEntryId = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private class StubPdfRenderer : IPdfInvoiceRenderer
    {
        public Task<byte[]> RenderAsync(InvoiceDocumentModel model, CancellationToken ct) => Task.FromResult(new byte[] { 1, 2, 3 });
    }

    private class ThrowingRateProvider : ICurrencyRateProvider
    {
        public Task<decimal?> FetchRateAsync(string baseCurrency, string quoteCurrency, DateOnly date, CancellationToken ct) =>
            throw new InvalidOperationException("Not expected to be called for a GBP-only client.");
    }
}
