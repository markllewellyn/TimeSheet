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
/// locks the entries it was built from"), BuildDraftAsync's per-entry line generation, and the bulk
/// per-project discount. InvoiceLineItem snapshots StaffId/TaskDate/Description per entry but has no FK back
/// to the TimesheetEntry it came from, so FinalizeInvoiceAsync has to reconstitute the locked entry set by
/// re-running the same period/project query (ITimesheetEntryRepository.GetCountedForInvoicingAsync) before
/// stamping InvoiceId. Exercised end to end against a real database, not mocked.</summary>
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
            StartDate = new DateOnly(2026, 1, 1), IsActive = true, CanInvoice = true, CreatedUtc = DateTimeOffset.UtcNow,
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

    private static ExpenseEntry MakeExpense(Project project, User user, DateOnly date, decimal amount) => new()
    {
        UserId = user.Id, ProjectId = project.Id, Date = date, Amount = amount, Currency = "GBP",
        Description = "Taxi", IsBillable = true, CreatedUtc = DateTimeOffset.UtcNow,
    };

    private static InvoicingService CreateService(TimesheetDbContext db) => new(
        new InvoiceRepository(db), new ClientRepository(db), new TimesheetEntryRepository(db), new ExpenseEntryRepository(db),
        new InvoiceGenerationService(
            new ClientRepository(db), new ProjectRepository(db), new TimesheetEntryRepository(db),
            new ExpenseEntryRepository(db), new CurrencyConversionService(new CurrencyRateRepository(db), new ThrowingRateProvider(), db)),
        new StubPdfRenderer(), new RecordingNotificationService(), db, new InMemoryFileStorageService());

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

    [Fact]
    public async Task GenerateDraftInvoiceAsync_EntryAlreadyLockedToAPriorInvoice_NotIncludedAgain()
    {
        // Reproduces a real bug the user hit live: finalizing an invoice, then generating a second draft for
        // the same client and an overlapping period, included the exact same already-invoiced entries again -
        // which would double-bill the client if that second draft were also finalized.
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedAsync(db);
        var entry = MakeEntry(client, project, user, new DateOnly(2026, 2, 15), 4m, 100m);
        db.TimesheetEntries.Add(entry);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var firstDraft = await service.GenerateDraftInvoiceAsync(client.Id, PeriodStart, PeriodEnd, null, CancellationToken.None);
        await service.FinalizeInvoiceAsync(firstDraft.Id, "INV-LOCK-1", user.Id, CancellationToken.None);

        var secondDraft = await service.GenerateDraftInvoiceAsync(client.Id, PeriodStart, PeriodEnd, null, CancellationToken.None);

        Assert.Empty(secondDraft.LineItems);
        Assert.Equal(0m, secondDraft.TotalAmount);
    }

    [Fact]
    public async Task FinalizeInvoiceAsync_Expense_LocksItAndExcludesItFromALaterDraft()
    {
        // Expenses had NO locking mechanism at all before this fix (no InvoiceId, no exclusion in
        // GetBillableForProjectAsync) - unlike timesheet entries, so this needed its own dedicated coverage.
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedAsync(db);
        var expense = MakeExpense(project, user, new DateOnly(2026, 2, 10), 50m);
        db.ExpenseEntries.Add(expense);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var firstDraft = await service.GenerateDraftInvoiceAsync(client.Id, PeriodStart, PeriodEnd, null, CancellationToken.None);
        Assert.Single(firstDraft.LineItems, l => l.Type == InvoiceLineItemType.Expense);

        await service.FinalizeInvoiceAsync(firstDraft.Id, "INV-EXP-1", user.Id, CancellationToken.None);

        var reloaded = await db.ExpenseEntries.AsNoTracking().SingleAsync(e => e.Id == expense.Id);
        Assert.NotNull(reloaded.InvoiceId);

        var secondDraft = await service.GenerateDraftInvoiceAsync(client.Id, PeriodStart, PeriodEnd, null, CancellationToken.None);
        Assert.Empty(secondDraft.LineItems);
    }

    [Fact]
    public async Task GenerateDraftInvoiceAsync_TimeAndMaterials_OneLineItemPerEntry()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedAsync(db);
        db.TimesheetEntries.AddRange(
            MakeEntry(client, project, user, new DateOnly(2026, 2, 3), 4m, 100m),
            MakeEntry(client, project, user, new DateOnly(2026, 2, 10), 2m, 100m),
            MakeEntry(client, project, user, new DateOnly(2026, 2, 17), 6m, 100m));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var draft = await service.GenerateDraftInvoiceAsync(client.Id, PeriodStart, PeriodEnd, null, CancellationToken.None);

        Assert.Equal(3, draft.LineItems.Count);
        Assert.All(draft.LineItems, l =>
        {
            Assert.Equal(user.Id, l.StaffId);
            Assert.Equal("Staff", l.StaffName);
            Assert.Equal("Work", l.Description);
            Assert.Equal(100m, l.Rate);
        });
        Assert.Equal(1200m, draft.TotalAmount); // (4+2+6)*100
    }

    [Fact]
    public async Task ApplyProjectDiscountAsync_DiscountsEveryLineForThatProjectAndRecomputesTotal()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, user) = await SeedAsync(db);
        db.TimesheetEntries.AddRange(
            MakeEntry(client, project, user, new DateOnly(2026, 2, 3), 4m, 100m),
            MakeEntry(client, project, user, new DateOnly(2026, 2, 10), 2m, 100m));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var draft = await service.GenerateDraftInvoiceAsync(client.Id, PeriodStart, PeriodEnd, null, CancellationToken.None);

        var discounted = await service.ApplyProjectDiscountAsync(draft.Id, project.Id, 10m, CancellationToken.None);

        Assert.All(discounted.LineItems, l => Assert.Equal(10m, l.DiscountPercent));
        Assert.Equal(540m, discounted.TotalAmount); // (400+200) * 0.9
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

    private class InMemoryFileStorageService : IFileStorageService
    {
        private readonly Dictionary<string, byte[]> blobs = new();

        public Task<string> SaveAsync(string suggestedFileName, Stream content, CancellationToken ct)
        {
            using var buffer = new MemoryStream();
            content.CopyTo(buffer);
            var key = $"{Guid.NewGuid():N}-{suggestedFileName}";
            blobs[key] = buffer.ToArray();
            return Task.FromResult(key);
        }

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream(blobs[storageKey]));

        public Task DeleteAsync(string storageKey, CancellationToken ct)
        {
            blobs.Remove(storageKey);
            return Task.CompletedTask;
        }
    }

    private class ThrowingRateProvider : ICurrencyRateProvider
    {
        public Task<decimal?> FetchRateAsync(string baseCurrency, string quoteCurrency, DateOnly date, CancellationToken ct) =>
            throw new InvalidOperationException("Not expected to be called for a GBP-only client.");
    }
}
