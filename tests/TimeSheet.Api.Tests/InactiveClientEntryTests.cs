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

/// <summary>New time and expense entries are refused against a deactivated client (InactiveClientGuard), but
/// existing entries on one can still be edited - deactivating a client stops new work, it doesn't freeze the
/// record of old work.</summary>
public class InactiveClientEntryTests
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

    /// <summary>Priya, assigned to POS Rollout at BrightPath, which is deactivated.</summary>
    private static async Task<(Client Client, Project Project, User Priya)> SeedAsync(TimesheetDbContext db, bool clientActive)
    {
        var client = new Client { Name = "BrightPath", AccountCode = "C00001", StartDate = new DateOnly(2026, 1, 1), CreatedUtc = DateTimeOffset.UtcNow, IsActive = clientActive };
        var priya = new User { EntraObjectId = "oid-priya", Email = "priya@svgit.co.uk", DisplayName = "Priya Patel", PayrollNumber = "P0004", CreatedUtc = DateTimeOffset.UtcNow };
        db.Clients.Add(client);
        db.Users.Add(priya);
        await db.SaveChangesAsync();

        var project = new Project
        {
            ClientId = client.Id, Name = "POS Rollout", Code = "POS", PaymentModel = PaymentModel.TimeAndMaterials,
            StartDate = new DateOnly(2026, 1, 1), IsActive = true, CreatedUtc = DateTimeOffset.UtcNow, CanInvoice = true,
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        db.StaffProjects.Add(new StaffProject { StaffId = priya.Id, ProjectId = project.Id, StartDate = new DateOnly(2026, 1, 1), IsActive = true, CreatedUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        return (client, project, priya);
    }

    private static ICurrentUserAccessor As(User user) =>
        new StubCurrentUserAccessor(new CurrentUserContext(user.Id, user.EntraObjectId!, user.Email, user.DisplayName, UserRole.User));

    private static ExpenseEntriesFunctions ExpenseFunctions(TimesheetDbContext db, User user) => new(
        new ExpenseEntryRepository(db), new StaffProjectRepository(db), new ProjectRepository(db), new UserRepository(db),
        new AuditLogService(new AuditLogRepository(db), new UserRepository(db)), db, As(user));

    /// <summary>Only the dependencies the refusal path reaches are real. Rates, budget monitoring, flags and
    /// notifications run after the guard, so a refused request never touches them - were the guard missing, the
    /// test would still fail (on the null), just less tidily.</summary>
    private static TimesheetEntriesFunctions TimeFunctions(TimesheetDbContext db, User user) => new(
        new TimesheetEntryRepository(db), new StaffProjectRepository(db), new ProjectRepository(db), new ClientRepository(db),
        new EntryTypeRepository(db), new UserRepository(db), new EntryFlagRepository(db),
        rateResolver: null!, budgetMonitoring: null!, entryFlagService: null!, notificationService: null!,
        new AuditLogService(new AuditLogRepository(db), new UserRepository(db)), db, As(user));

    private static string ErrorOf(IActionResult result) =>
        JsonSerializer.Serialize(Assert.IsType<BadRequestObjectResult>(result).Value);

    [Fact]
    public async Task TimeEntryCreate_InactiveClient_IsRefused()
    {
        await using var db = CreateInMemoryDb();
        var (_, project, priya) = await SeedAsync(db, clientActive: false);

        var result = await TimeFunctions(db, priya).Create(BuildJsonRequest(new
        {
            projectId = project.Id, date = "2026-09-01", workHours = 2m, outOfHoursHours = 0m, description = "Till setup",
        }), CancellationToken.None);

        Assert.Contains("BrightPath is inactive", ErrorOf(result));
        Assert.False(await db.TimesheetEntries.AnyAsync());
    }

    [Fact]
    public async Task TimeEntryDuplicate_InactiveClient_IsRefused()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, priya) = await SeedAsync(db, clientActive: true);
        var existing = new TimesheetEntry
        {
            UserId = priya.Id, ClientId = client.Id, ProjectId = project.Id, Date = new DateOnly(2026, 8, 1),
            WorkHours = 2m, Description = "Till setup", CreatedUtc = DateTimeOffset.UtcNow,
        };
        db.TimesheetEntries.Add(existing);
        await db.SaveChangesAsync();
        client.IsActive = false; // deactivated after the entry was logged
        await db.SaveChangesAsync();

        var result = await TimeFunctions(db, priya).Duplicate(BuildJsonRequest(new { date = "2026-09-01" }), existing.Id, CancellationToken.None);

        Assert.Contains("BrightPath is inactive", ErrorOf(result));
        Assert.Equal(1, await db.TimesheetEntries.CountAsync());
    }

    [Fact]
    public async Task ExpenseCreate_InactiveClient_IsRefused()
    {
        await using var db = CreateInMemoryDb();
        var (_, project, priya) = await SeedAsync(db, clientActive: false);

        var result = await ExpenseFunctions(db, priya).Create(BuildJsonRequest(new
        {
            projectId = project.Id, date = "2026-09-01", amount = 10m, currency = "GBP", description = "Meal", isBillable = true,
        }), CancellationToken.None);

        Assert.Contains("BrightPath is inactive", ErrorOf(result));
        Assert.False(await db.ExpenseEntries.AnyAsync());
    }

    [Fact]
    public async Task ExpenseCreate_ActiveClient_StillWorks()
    {
        await using var db = CreateInMemoryDb();
        var (_, project, priya) = await SeedAsync(db, clientActive: true);

        var result = await ExpenseFunctions(db, priya).Create(BuildJsonRequest(new
        {
            projectId = project.Id, date = "2026-09-01", amount = 10m, currency = "GBP", description = "Meal", isBillable = true,
        }), CancellationToken.None);

        Assert.IsType<CreatedResult>(result);
        Assert.Equal(1, await db.ExpenseEntries.CountAsync());
    }

    [Fact]
    public async Task ExpenseUpdate_InactiveClient_ExistingEntryCanStillBeEdited()
    {
        await using var db = CreateInMemoryDb();
        var (client, project, priya) = await SeedAsync(db, clientActive: true);
        var expense = new ExpenseEntry
        {
            UserId = priya.Id, ProjectId = project.Id, Date = new DateOnly(2026, 8, 1), Amount = 10m, Currency = "GBP",
            Description = "Meal", IsBillable = true, CreatedUtc = DateTimeOffset.UtcNow,
        };
        db.ExpenseEntries.Add(expense);
        await db.SaveChangesAsync();
        client.IsActive = false;
        await db.SaveChangesAsync();

        var result = await ExpenseFunctions(db, priya).Update(BuildJsonRequest(new
        {
            date = "2026-08-01", amount = 12m, currency = "GBP", description = "Meal (corrected)", isBillable = true,
        }), expense.Id, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        db.ChangeTracker.Clear();
        Assert.Equal(12m, (await db.ExpenseEntries.SingleAsync()).Amount);
    }
}
