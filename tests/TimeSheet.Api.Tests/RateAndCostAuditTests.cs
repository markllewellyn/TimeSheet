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

/// <summary>FDD architecture: "row-level audit trail on rates, entries and invoices". Rate cards (including a
/// client page's person Rate Overrides) and staff costs are only ever created - a change is a new dated row - so
/// each Create must leave a SAVED audit row. ChangeTracker.Clear() before each assertion discards anything that
/// was only staged, which is the trap InvoicesFunctions fell into.</summary>
public class RateAndCostAuditTests
{
    private static TimesheetDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<TimesheetDbContext>().UseSqlite("Data Source=:memory:").Options;
        var db = new TimesheetDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    private class StubCurrentUserAccessor(CurrentUserContext user) : ICurrentUserAccessor
    {
        public CurrentUserContext? Current => user;
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

    private static async Task<(User Admin, User Staff, Client Client)> SeedAsync(TimesheetDbContext db)
    {
        var admin = new User { EntraObjectId = "oid-admin", Email = "admin@svgit.co.uk", DisplayName = "Admin User", PayrollNumber = "P0001", CreatedUtc = DateTimeOffset.UtcNow };
        var staff = new User { EntraObjectId = "oid-james", Email = "james@svgit.co.uk", DisplayName = "James O'Brien", PayrollNumber = "P0002", CreatedUtc = DateTimeOffset.UtcNow };
        var client = new Client { Name = "Northwind", AccountCode = "C00001", StartDate = new DateOnly(2026, 6, 1), CreatedUtc = DateTimeOffset.UtcNow };
        db.Users.AddRange(admin, staff);
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return (admin, staff, client);
    }

    private static IAuditLogService AuditLog(TimesheetDbContext db) => new AuditLogService(new AuditLogRepository(db), new UserRepository(db));

    private static ICurrentUserAccessor AsAdmin(User admin) =>
        new StubCurrentUserAccessor(new CurrentUserContext(admin.Id, admin.EntraObjectId!, admin.Email, admin.DisplayName, UserRole.Admin));

    [Fact]
    public async Task RateCardCreate_PersonAtClientOverride_WritesASavedAuditRow()
    {
        await using var db = CreateInMemoryDb();
        var (admin, staff, client) = await SeedAsync(db);
        var functions = new RateCardsFunctions(new RateCardRepository(db), new RoleRepository(db), new UserRepository(db),
            new ClientRepository(db), new ProjectRepository(db), AuditLog(db), db, AsAdmin(admin));

        var result = await functions.Create(BuildJsonRequest(new
        {
            staffId = staff.Id, clientId = client.Id, rate = 110m, discountPercent = 10m, effectiveFrom = "2026-06-01",
        }), CancellationToken.None);

        Assert.IsType<CreatedResult>(result);
        db.ChangeTracker.Clear();
        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "RateCard.Created");
        Assert.Equal(admin.Id, audit.UserId);
        Assert.Equal("Person James O'Brien, client Northwind: 110.00/h, 10% discount, effective 2026-06-01", audit.Details);
        Assert.Equal((await db.RateCards.SingleAsync()).Id, audit.EntityId);
    }

    [Fact]
    public async Task RateCardCreate_RoleDefault_DescribesTheRole()
    {
        await using var db = CreateInMemoryDb();
        var (admin, _, _) = await SeedAsync(db);
        var role = new Role { Name = "Consultant", CreatedUtc = DateTimeOffset.UtcNow };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var functions = new RateCardsFunctions(new RateCardRepository(db), new RoleRepository(db), new UserRepository(db),
            new ClientRepository(db), new ProjectRepository(db), AuditLog(db), db, AsAdmin(admin));

        await functions.Create(BuildJsonRequest(new { roleId = role.Id, rate = 120m, effectiveFrom = "2026-09-04" }), CancellationToken.None);

        db.ChangeTracker.Clear();
        Assert.Equal("Role Consultant, all clients: 120.00/h, effective 2026-09-04",
            (await db.AuditLogs.SingleAsync(a => a.Action == "RateCard.Created")).Details);
    }

    [Fact]
    public async Task StaffCostCreate_WritesASavedAuditRow()
    {
        await using var db = CreateInMemoryDb();
        var (admin, staff, _) = await SeedAsync(db);
        var functions = new StaffCostsFunctions(new StaffCostRepository(db), new UserRepository(db), AuditLog(db), db, AsAdmin(admin));

        var result = await functions.Create(BuildJsonRequest(new
        {
            staffId = staff.Id, hourlyCost = 42.5m, outOfHoursCost = 165m, effectiveFrom = "2026-06-01",
        }), CancellationToken.None);

        Assert.IsType<CreatedResult>(result);
        db.ChangeTracker.Clear();
        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "StaffCost.Created");
        Assert.Equal("James O'Brien: hourly 42.50, out of hours 165.00, effective 2026-06-01", audit.Details);
        Assert.Equal((await db.StaffCosts.SingleAsync()).Id, audit.EntityId);
    }

    [Fact]
    public async Task RateCardCreate_RejectedRequest_WritesNoAuditRow()
    {
        await using var db = CreateInMemoryDb();
        var (admin, _, _) = await SeedAsync(db);
        var functions = new RateCardsFunctions(new RateCardRepository(db), new RoleRepository(db), new UserRepository(db),
            new ClientRepository(db), new ProjectRepository(db), AuditLog(db), db, AsAdmin(admin));

        // Neither roleId nor staffId - a 400 before anything is saved.
        var result = await functions.Create(BuildJsonRequest(new { rate = 120m, effectiveFrom = "2026-09-04" }), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(await db.AuditLogs.AnyAsync());
    }
}
