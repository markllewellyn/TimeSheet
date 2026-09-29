using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeSheet.Api.Functions;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Services;
using TimeSheet.Infrastructure.Data;
using TimeSheet.Infrastructure.Repositories;
using TimeSheet.Infrastructure.Services;
using Xunit;

namespace TimeSheet.Api.Tests;

/// <summary>Invoice actions must leave a SAVED AuditLog row. Before 2026-09-29 InvoicesFunctions staged
/// Invoice.Voided/Invoice.DraftDeleted via IAuditLogService but never saved them (the dev DB had 11 voided
/// invoices and zero Voided audit rows), and Generate/Finalize/discounts weren't audited at all. The invoicing
/// service is faked - it saves its own changes first, exactly like the real InvoicingService - so these tests
/// isolate the Functions layer's own audit-and-save step.</summary>
public class InvoicesFunctionsTests
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

    private class StubCurrentUserAccessor(CurrentUserContext user) : ICurrentUserAccessor
    {
        public CurrentUserContext? Current => user;
    }

    /// <summary>Just enough of IInvoicingService for these tests, mutating and saving the real tracked invoice.</summary>
    private class FakeInvoicingService(TimesheetDbContext db) : IInvoicingService
    {
        public async Task<Invoice> VoidInvoiceAsync(int invoiceId, string? reason, int voidedByUserId, string voidedByName, CancellationToken ct)
        {
            var invoice = await db.Invoices.SingleAsync(i => i.Id == invoiceId, ct);
            invoice.Status = InvoiceStatus.Voided;
            invoice.VoidedAtUtc = DateTimeOffset.UtcNow;
            invoice.VoidedByUserId = voidedByUserId;
            invoice.VoidedByName = voidedByName;
            invoice.VoidReason = reason;
            await db.SaveChangesAsync(ct);
            return invoice;
        }

        public async Task<Invoice> ApplyLineItemDiscountAsync(int invoiceId, int lineItemId, decimal? discountPercent, CancellationToken ct)
        {
            var invoice = await db.Invoices.Include(i => i.LineItems).SingleAsync(i => i.Id == invoiceId, ct);
            var line = invoice.LineItems.Single(l => l.Id == lineItemId);
            line.DiscountPercent = discountPercent;
            line.Amount = line.GrossAmount * (1 - (discountPercent ?? 0) / 100m);
            invoice.TotalAmount = invoice.LineItems.Sum(l => l.Amount);
            await db.SaveChangesAsync(ct);
            return invoice;
        }

        public Task<Invoice> GenerateDraftInvoiceAsync(int clientId, DateOnly periodStart, DateOnly periodEnd, decimal? manualExchangeRate, CancellationToken ct) => throw new NotImplementedException();
        public Task<Invoice> ApplyProjectDiscountAsync(int invoiceId, int projectId, decimal? discountPercent, CancellationToken ct) => throw new NotImplementedException();
        public Task<Invoice> FinalizeInvoiceAsync(int invoiceId, string invoiceNumber, int finalizedByUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task<Stream> GetPdfAsync(int invoiceId, CancellationToken ct) => throw new NotImplementedException();
        public Task DeleteDraftAsync(int invoiceId, CancellationToken ct) => throw new NotImplementedException();
    }

    private static HttpRequest BuildJsonRequest(object body)
    {
        var context = new DefaultHttpContext();
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = bytes.Length;
        return context.Request;
    }

    private static async Task<(InvoicesFunctions Functions, Invoice Invoice, InvoiceLineItem Line)> SeedAsync(TimesheetDbContext db)
    {
        var admin = new User { EntraObjectId = "oid-admin", Email = "admin@svgit.co.uk", DisplayName = "Admin User", PayrollNumber = "P0001", CreatedUtc = DateTimeOffset.UtcNow };
        var client = new Client { Name = "Northwind", AccountCode = "C00001", StartDate = new DateOnly(2026, 6, 1), CreatedUtc = DateTimeOffset.UtcNow };
        db.Users.Add(admin);
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var project = new Project
        {
            ClientId = client.Id, Name = "Warehouse Ops", Code = "WH", PaymentModel = PaymentModel.TimeAndMaterials,
            StartDate = new DateOnly(2026, 6, 1), IsActive = true, CreatedUtc = DateTimeOffset.UtcNow, CanInvoice = true,
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var line = new InvoiceLineItem
        {
            ProjectId = project.Id, Description = "Consulting", Hours = 10m, Rate = 100m, GrossAmount = 1000m, Amount = 1000m,
            Type = InvoiceLineItemType.TimeAndMaterials,
        };
        var invoice = new Invoice
        {
            ClientId = client.Id, PeriodStart = new DateOnly(2026, 6, 1), PeriodEnd = new DateOnly(2026, 6, 30), ReportingCurrency = "GBP",
            Status = InvoiceStatus.Finalized, InvoiceNumber = "INV-001", TotalAmount = 1000m, GeneratedAtUtc = DateTimeOffset.UtcNow,
            LineItems = [line],
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var functions = new InvoicesFunctions(
            new InvoiceRepository(db), new ProjectRepository(db), new FakeInvoicingService(db),
            new AuditLogService(new AuditLogRepository(db), new UserRepository(db)), db,
            new StubCurrentUserAccessor(new CurrentUserContext(admin.Id, admin.EntraObjectId!, admin.Email, admin.DisplayName, UserRole.Admin)));
        return (functions, invoice, line);
    }

    [Fact]
    public async Task Void_WritesASavedAuditRow_WithTheReason()
    {
        await using var db = CreateInMemoryDb();
        var (functions, invoice, _) = await SeedAsync(db);

        var result = await functions.Void(BuildJsonRequest(new { reason = "Wrong period" }), invoice.Id, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        db.ChangeTracker.Clear(); // anything staged but never saved is discarded here
        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "Invoice.Voided");
        Assert.Equal(invoice.Id, audit.EntityId);
        Assert.Equal("Wrong period", audit.Details);
    }

    [Fact]
    public async Task ApplyLineItemDiscount_WritesASavedAuditRow_ShowingOldAndNewDiscount()
    {
        await using var db = CreateInMemoryDb();
        var (functions, invoice, line) = await SeedAsync(db);

        var result = await functions.ApplyLineItemDiscount(BuildJsonRequest(new { discountPercent = 10m }), invoice.Id, line.Id, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        db.ChangeTracker.Clear();
        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "Invoice.LineDiscountChanged");
        Assert.Equal($"line {line.Id}: none -> 10%, new total 900.00", audit.Details);
    }
}
