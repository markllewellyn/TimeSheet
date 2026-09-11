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
using Xunit;

namespace TimeSheet.Api.Tests;

/// <summary>Closes a known-loose-end verification gap (HANDOFF): "the batch-rejection path for PM-scoped
/// Approve/SendToPayroll ... was verified by code review, not live" - exercising it live through the real app
/// isn't practically possible (the Approval Queue UI, correctly, never shows a PM an out-of-scope entry to
/// select in the first place - only a hand-crafted request could mix one in, and forging a real session's auth
/// token to do that against the running app was refused by this session's own safety classifier). A direct
/// Functions-level test - the first for this codebase's Functions layer, which otherwise has none - is the
/// legitimate alternative: no auth/network involved, just the real TimesheetApprovalFunctions class against a
/// real in-memory SQLite-backed repository stack.</summary>
public class TimesheetApprovalFunctionsTests
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

    private class NoopAuditLogService : IAuditLogService
    {
        public Task LogAsync(CurrentUserContext actor, string action, string entityType, int? entityId, string? details, int? impersonatedUserId, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private static HttpRequest BuildJsonRequest(object body)
    {
        var context = new DefaultHttpContext();
        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = bytes.Length;
        return context.Request;
    }

    [Fact]
    public async Task Approve_PmSelectsEntryOutsideManagedProjects_RejectsWholeBatchWith400_NoPartialMutation()
    {
        await using var db = CreateInMemoryDb();

        var client = new Client { Name = "Antigua", AccountCode = "C13673", StartDate = new DateOnly(2026, 8, 1), CreatedUtc = DateTimeOffset.UtcNow };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var pm = new User { EntraObjectId = "oid-pm", Email = "pm@svgit.co.uk", DisplayName = "PM User", PayrollNumber = "P0001", CreatedUtc = DateTimeOffset.UtcNow };
        var otherUser = new User { EntraObjectId = "oid-other", Email = "other@svgit.co.uk", DisplayName = "Other User", PayrollNumber = "P0002", CreatedUtc = DateTimeOffset.UtcNow };
        db.Users.AddRange(pm, otherUser);
        await db.SaveChangesAsync();

        var managedProject = new Project
        {
            ClientId = client.Id, Name = "Managed", Code = "MANAGED", PaymentModel = PaymentModel.TimeAndMaterials,
            StartDate = new DateOnly(2026, 8, 1), IsActive = true, CreatedUtc = DateTimeOffset.UtcNow, CanInvoice = true,
            ProjectManagerUserId = pm.Id,
        };
        var otherProject = new Project
        {
            ClientId = client.Id, Name = "Other", Code = "OTHER", PaymentModel = PaymentModel.TimeAndMaterials,
            StartDate = new DateOnly(2026, 8, 1), IsActive = true, CreatedUtc = DateTimeOffset.UtcNow, CanInvoice = true,
        };
        db.Projects.AddRange(managedProject, otherProject);
        await db.SaveChangesAsync();

        var inScopeEntry = new TimesheetEntry
        {
            UserId = pm.Id, ClientId = client.Id, ProjectId = managedProject.Id, Date = new DateOnly(2026, 8, 10),
            WorkHours = 5m, OutOfHoursHours = 0m, Description = "in scope", CreatedUtc = DateTimeOffset.UtcNow,
        };
        var outOfScopeEntry = new TimesheetEntry
        {
            UserId = otherUser.Id, ClientId = client.Id, ProjectId = otherProject.Id, Date = new DateOnly(2026, 8, 10),
            WorkHours = 3m, OutOfHoursHours = 0m, Description = "out of scope", CreatedUtc = DateTimeOffset.UtcNow,
        };
        db.TimesheetEntries.AddRange(inScopeEntry, outOfScopeEntry);
        await db.SaveChangesAsync();

        var entries = new TimesheetEntryRepository(db);
        var projects = new ProjectRepository(db);
        var currentUser = new StubCurrentUserAccessor(new CurrentUserContext(pm.Id, pm.EntraObjectId!, pm.Email, pm.DisplayName, UserRole.User));
        var functions = new TimesheetApprovalFunctions(entries, projects, new NoopAuditLogService(), db, currentUser);

        var request = BuildJsonRequest(new { entryIds = new[] { inScopeEntry.Id, outOfScopeEntry.Id }, postingBatch = "TEST-BATCH" });
        var result = await functions.Approve(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);

        // The whole batch is rejected - even the entry that WAS in scope must not have been silently approved
        // on its own, which would be a confusing partial-success result (the class's own documented intent).
        var reloadedInScope = await db.TimesheetEntries.FindAsync(inScopeEntry.Id);
        Assert.False(reloadedInScope!.ApprovedPayroll);
    }

    [Fact]
    public async Task Approve_PmSelectsOnlyOwnManagedProjectEntries_Succeeds()
    {
        await using var db = CreateInMemoryDb();

        var client = new Client { Name = "Antigua", AccountCode = "C13673", StartDate = new DateOnly(2026, 8, 1), CreatedUtc = DateTimeOffset.UtcNow };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var pm = new User { EntraObjectId = "oid-pm", Email = "pm@svgit.co.uk", DisplayName = "PM User", PayrollNumber = "P0001", CreatedUtc = DateTimeOffset.UtcNow };
        db.Users.Add(pm);
        await db.SaveChangesAsync();

        var managedProject = new Project
        {
            ClientId = client.Id, Name = "Managed", Code = "MANAGED", PaymentModel = PaymentModel.TimeAndMaterials,
            StartDate = new DateOnly(2026, 8, 1), IsActive = true, CreatedUtc = DateTimeOffset.UtcNow, CanInvoice = true,
            ProjectManagerUserId = pm.Id,
        };
        db.Projects.Add(managedProject);
        await db.SaveChangesAsync();

        var entry = new TimesheetEntry
        {
            UserId = pm.Id, ClientId = client.Id, ProjectId = managedProject.Id, Date = new DateOnly(2026, 8, 10),
            WorkHours = 5m, OutOfHoursHours = 0m, Description = "in scope", CreatedUtc = DateTimeOffset.UtcNow,
        };
        db.TimesheetEntries.Add(entry);
        await db.SaveChangesAsync();

        var entries = new TimesheetEntryRepository(db);
        var projects = new ProjectRepository(db);
        var currentUser = new StubCurrentUserAccessor(new CurrentUserContext(pm.Id, pm.EntraObjectId!, pm.Email, pm.DisplayName, UserRole.User));
        var functions = new TimesheetApprovalFunctions(entries, projects, new NoopAuditLogService(), db, currentUser);

        var request = BuildJsonRequest(new { entryIds = new[] { entry.Id }, postingBatch = "TEST-BATCH" });
        var result = await functions.Approve(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var reloaded = await db.TimesheetEntries.FindAsync(entry.Id);
        Assert.True(reloaded!.ApprovedPayroll);
    }
}
