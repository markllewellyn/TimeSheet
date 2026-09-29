using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TimeSheet.Api.Functions;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Services;
using TimeSheet.Infrastructure.Data;
using TimeSheet.Infrastructure.Repositories;
using Xunit;

namespace TimeSheet.Api.Tests;

/// <summary>Clients_Reactivate - the undo for Clients_Deactivate. Before it existed a deactivated client could
/// never be brought back from the app, and an inactive client silently drops out of the Reports client list.</summary>
public class ClientsFunctionsTests
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

    private static async Task<(TimesheetDbContext Db, Client Client, User User)> SeedInactiveClientAsync()
    {
        var db = CreateInMemoryDb();
        var user = new User { EntraObjectId = "oid-user", Email = "user@svgit.co.uk", DisplayName = "Some User", PayrollNumber = "P0001", CreatedUtc = DateTimeOffset.UtcNow };
        var client = new Client { Name = "Bright", AccountCode = "C00001", StartDate = new DateOnly(2026, 8, 1), CreatedUtc = DateTimeOffset.UtcNow, IsActive = false };
        db.Users.Add(user);
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return (db, client, user);
    }

    private static ClientsFunctions CreateFunctions(TimesheetDbContext db, User user, UserRole role) => new(
        new ClientRepository(db), new CurrencyRepository(db), db,
        new StubCurrentUserAccessor(new CurrentUserContext(user.Id, user.EntraObjectId!, user.Email, user.DisplayName, role)),
        NullLogger<ClientsFunctions>.Instance);

    [Fact]
    public async Task Reactivate_AsAdmin_MakesClientActiveAgain_AndItReappearsInTheActiveList()
    {
        var (db, client, user) = await SeedInactiveClientAsync();
        await using var _ = db;
        var functions = CreateFunctions(db, user, UserRole.Admin);

        var result = await functions.Reactivate(new DefaultHttpContext().Request, client.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        db.ChangeTracker.Clear();
        var reloaded = await db.Clients.SingleAsync(c => c.Id == client.Id);
        Assert.True(reloaded.IsActive);
        Assert.Equal(user.Id, reloaded.ModifiedByUserId);

        // The Reports page's client dropdown uses the default (active-only) list.
        var list = Assert.IsType<OkObjectResult>(await functions.List(new DefaultHttpContext().Request, CancellationToken.None));
        Assert.Contains(client.Name, System.Text.Json.JsonSerializer.Serialize(list.Value));
    }

    [Fact]
    public async Task Reactivate_AsNonAdmin_IsRefused_AndClientStaysInactive()
    {
        var (db, client, user) = await SeedInactiveClientAsync();
        await using var _ = db;
        var functions = CreateFunctions(db, user, UserRole.User);

        var result = await functions.Reactivate(new DefaultHttpContext().Request, client.Id, CancellationToken.None);

        Assert.IsNotType<NoContentResult>(result);
        db.ChangeTracker.Clear();
        Assert.False((await db.Clients.SingleAsync(c => c.Id == client.Id)).IsActive);
    }

    [Fact]
    public async Task Reactivate_UnknownClient_Returns404()
    {
        var (db, _, user) = await SeedInactiveClientAsync();
        await using var __ = db;
        var functions = CreateFunctions(db, user, UserRole.Admin);

        var result = await functions.Reactivate(new DefaultHttpContext().Request, 999, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
