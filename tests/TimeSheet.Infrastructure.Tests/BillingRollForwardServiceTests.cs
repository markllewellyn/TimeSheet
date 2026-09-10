using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Services;
using TimeSheet.Infrastructure.Data;
using TimeSheet.Infrastructure.Repositories;
using TimeSheet.Infrastructure.Services;
using Xunit;

namespace TimeSheet.Infrastructure.Tests;

public class BillingRollForwardServiceTests
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

    private static Client MakeMonthlyClient(string name, DateOnly currentPeriodStart, DateOnly currentPeriodEnd, bool isActive = true) => new()
    {
        Name = name, AccountCode = name, StartDate = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow,
        BillingPeriod = BillingPeriod.Monthly, CurrentPeriodStart = currentPeriodStart, CurrentPeriodEnd = currentPeriodEnd,
        IsActive = isActive,
    };

    private static BillingRollForwardService CreateService(TimesheetDbContext db, int? failingClientId = null)
    {
        IInvoicingService invoicing = new InvoicingService(
            new InvoiceRepository(db),
            new ClientRepository(db),
            new TimesheetEntryRepository(db),
            new ExpenseEntryRepository(db),
            new InvoiceGenerationService(
                new ClientRepository(db), new ProjectRepository(db), new TimesheetEntryRepository(db),
                new ExpenseEntryRepository(db), new CurrencyConversionService(new CurrencyRateRepository(db), new ThrowingRateProvider(), db)),
            new ThrowingPdfRenderer(),
            new RecordingNotificationService(),
            db,
            new InMemoryFileStorageService());

        if (failingClientId is { } clientId) invoicing = new SelectivelyFailingInvoicingService(invoicing, clientId);

        return new BillingRollForwardService(new ClientRepository(db), invoicing, new RecordingNotificationService(), db,
            NullLogger<BillingRollForwardService>.Instance);
    }

    [Fact]
    public async Task RunAsync_OverdueMonthlyClient_GeneratesDraftAndAdvancesPeriod()
    {
        await using var db = CreateInMemoryDb();
        var client = MakeMonthlyClient("Overdue Co", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var result = await CreateService(db).RunAsync(new DateOnly(2026, 2, 5), CancellationToken.None);

        Assert.Equal(1, result.ClientsDue);
        Assert.Equal(1, result.InvoicesGenerated);
        Assert.Equal(0, result.Failed);

        var invoice = await db.Invoices.SingleAsync(i => i.ClientId == client.Id);
        Assert.Equal(new DateOnly(2026, 1, 1), invoice.PeriodStart);
        Assert.Equal(new DateOnly(2026, 1, 31), invoice.PeriodEnd);
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);

        var reloaded = await db.Clients.AsNoTracking().SingleAsync(c => c.Id == client.Id);
        Assert.Equal(new DateOnly(2026, 2, 1), reloaded.CurrentPeriodStart);
        Assert.Equal(new DateOnly(2026, 2, 28), reloaded.CurrentPeriodEnd);
    }

    [Fact]
    public async Task RunAsync_NotYetDueMonthlyClient_Untouched()
    {
        await using var db = CreateInMemoryDb();
        var client = MakeMonthlyClient("Not Due Co", new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var result = await CreateService(db).RunAsync(new DateOnly(2026, 2, 5), CancellationToken.None);

        Assert.Equal(0, result.ClientsDue);
        Assert.Equal(0, await db.Invoices.CountAsync());

        var reloaded = await db.Clients.AsNoTracking().SingleAsync(c => c.Id == client.Id);
        Assert.Equal(new DateOnly(2026, 2, 1), reloaded.CurrentPeriodStart);
        Assert.Equal(new DateOnly(2026, 2, 28), reloaded.CurrentPeriodEnd);
    }

    [Fact]
    public async Task RunAsync_OneOffClient_NeverTouchedRegardlessOfPeriodFields()
    {
        await using var db = CreateInMemoryDb();
        var client = new Client
        {
            Name = "OneOff Co", AccountCode = "OneOff Co", StartDate = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow,
            BillingPeriod = BillingPeriod.OneOff,
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var result = await CreateService(db).RunAsync(new DateOnly(2026, 2, 5), CancellationToken.None);

        Assert.Equal(0, result.ClientsDue);
        Assert.Equal(0, await db.Invoices.CountAsync());
    }

    [Fact]
    public async Task RunAsync_InactiveClient_ExcludedEvenIfOverdue()
    {
        await using var db = CreateInMemoryDb();
        var client = MakeMonthlyClient("Inactive Co", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), isActive: false);
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var result = await CreateService(db).RunAsync(new DateOnly(2026, 2, 5), CancellationToken.None);

        Assert.Equal(0, result.ClientsDue);
        Assert.Equal(0, await db.Invoices.CountAsync());
    }

    [Fact]
    public async Task RunAsync_ReRunSameAsOfDate_DoesNotReTriggerAfterPeriodAdvanced()
    {
        await using var db = CreateInMemoryDb();
        var client = MakeMonthlyClient("Repeat Co", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var asOfDate = new DateOnly(2026, 2, 5);
        var first = await service.RunAsync(asOfDate, CancellationToken.None);
        var second = await service.RunAsync(asOfDate, CancellationToken.None);

        Assert.Equal(1, first.InvoicesGenerated);
        Assert.Equal(0, second.ClientsDue);
        Assert.Equal(1, await db.Invoices.CountAsync());
    }

    [Fact]
    public async Task RunAsync_OneClientThrows_OthersStillProcessed()
    {
        await using var db = CreateInMemoryDb();
        var goodClient = MakeMonthlyClient("Good Co", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        db.Clients.Add(goodClient);
        var badClient = MakeMonthlyClient("Bad Co", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        db.Clients.Add(badClient);
        await db.SaveChangesAsync();

        // Both clients are due (visible to GetAllAsync); the wrapper injects a failure only when generation is
        // attempted for badClient, isolating "one client's generation throws" from "one client isn't due."
        var result = await CreateService(db, failingClientId: badClient.Id).RunAsync(new DateOnly(2026, 2, 5), CancellationToken.None);

        Assert.Equal(2, result.ClientsDue);
        Assert.Equal(1, result.InvoicesGenerated);
        Assert.Equal(1, result.Failed);
        Assert.Equal(1, await db.Invoices.CountAsync());
        Assert.Equal(goodClient.Id, (await db.Invoices.SingleAsync()).ClientId);

        var reloadedGood = await db.Clients.AsNoTracking().SingleAsync(c => c.Id == goodClient.Id);
        Assert.Equal(new DateOnly(2026, 2, 1), reloadedGood.CurrentPeriodStart);
        var reloadedBad = await db.Clients.AsNoTracking().SingleAsync(c => c.Id == badClient.Id);
        Assert.Equal(new DateOnly(2026, 1, 1), reloadedBad.CurrentPeriodStart); // never advanced - generation failed
    }

    /// <summary>Forwards to a real IInvoicingService for every client except one designated failing id, letting
    /// tests isolate "GenerateDraftInvoiceAsync throws for this client" without disturbing GetAllAsync's view of
    /// which clients are due.</summary>
    private class SelectivelyFailingInvoicingService(IInvoicingService inner, int failingClientId) : IInvoicingService
    {
        public Task<Invoice> GenerateDraftInvoiceAsync(int clientId, DateOnly periodStart, DateOnly periodEnd, decimal? manualExchangeRate, CancellationToken ct) =>
            clientId == failingClientId
                ? throw new InvalidOperationException("Simulated failure for test.")
                : inner.GenerateDraftInvoiceAsync(clientId, periodStart, periodEnd, manualExchangeRate, ct);

        public Task<Invoice> ApplyLineItemDiscountAsync(int invoiceId, int lineItemId, decimal? discountPercent, CancellationToken ct) =>
            inner.ApplyLineItemDiscountAsync(invoiceId, lineItemId, discountPercent, ct);

        public Task<Invoice> ApplyProjectDiscountAsync(int invoiceId, int projectId, decimal? discountPercent, CancellationToken ct) =>
            inner.ApplyProjectDiscountAsync(invoiceId, projectId, discountPercent, ct);

        public Task<Invoice> FinalizeInvoiceAsync(int invoiceId, string invoiceNumber, int finalizedByUserId, CancellationToken ct) =>
            inner.FinalizeInvoiceAsync(invoiceId, invoiceNumber, finalizedByUserId, ct);

        public Task<Stream> GetPdfAsync(int invoiceId, CancellationToken ct) => inner.GetPdfAsync(invoiceId, ct);

        public Task DeleteDraftAsync(int invoiceId, CancellationToken ct) => inner.DeleteDraftAsync(invoiceId, ct);

        public Task<Invoice> VoidInvoiceAsync(int invoiceId, string? reason, int voidedByUserId, string voidedByName, CancellationToken ct) =>
            inner.VoidInvoiceAsync(invoiceId, reason, voidedByUserId, voidedByName, ct);
    }

    private class RecordingNotificationService : INotificationService
    {
        public Task RaiseAsync(int recipientUserId, NotificationType type, string message, NotificationChannel channel = NotificationChannel.Both,
            int? relatedProjectId = null, int? relatedTimesheetEntryId = null, CancellationToken ct = default) => Task.CompletedTask;

        public Task RaiseToAdminsAsync(NotificationType type, string message, NotificationChannel channel = NotificationChannel.Both,
            int? relatedProjectId = null, int? relatedTimesheetEntryId = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private class ThrowingPdfRenderer : IPdfInvoiceRenderer
    {
        public Task<byte[]> RenderAsync(InvoiceDocumentModel model, CancellationToken ct) =>
            throw new InvalidOperationException("Not expected to be called by GenerateDraftInvoiceAsync.");
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
            throw new InvalidOperationException("Not expected to be called for a GBP-only client with no projects.");
    }
}
