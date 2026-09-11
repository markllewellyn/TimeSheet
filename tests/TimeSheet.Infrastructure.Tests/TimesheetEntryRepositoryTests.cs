using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Infrastructure.Data;
using TimeSheet.Infrastructure.Repositories;
using Xunit;

namespace TimeSheet.Infrastructure.Tests;

public class TimesheetEntryRepositoryTests
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

    /// <summary>A real, previously-unreproducible bug report ("my own entry went missing from an Entry Flags
    /// search") - confirmed against live dev data that a broad search's take-25 cutoff routinely lands inside a
    /// run of same-date entries, where ordering by Date alone has no deterministic tiebreak. Seeds far more than
    /// 25 same-date, same-search-term matches so the cutoff is guaranteed to land mid-tie, then asserts the
    /// result is deterministic (same call twice returns the identical set/order) and, specifically, that it's
    /// the highest-Id (most recently created) rows that survive - not whatever SQLite's own tie order would
    /// otherwise pick.</summary>
    [Fact]
    public async Task SearchForFlaggingAsync_ManySameDateMatches_CutoffIsDeterministicAndFavorsMostRecentlyCreated()
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

        var user = new User
        {
            EntraObjectId = "oid-1", Email = "mark@svgit.co.uk", DisplayName = "Mark Llewellyn",
            PayrollNumber = "P0001", CreatedUtc = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // 30 entries, same date, same search term, inserted in ascending Id order - more than the take=25 cap.
        for (var i = 0; i < 30; i++)
        {
            db.TimesheetEntries.Add(new TimesheetEntry
            {
                UserId = user.Id, ClientId = client.Id, ProjectId = project.Id, Date = new DateOnly(2026, 8, 20),
                WorkHours = 1m, OutOfHoursHours = 0m, Description = $"flaggable entry {i}", CreatedUtc = DateTimeOffset.UtcNow,
            });
        }
        await db.SaveChangesAsync();

        var repo = new TimesheetEntryRepository(db);

        var first = await repo.SearchForFlaggingAsync("flaggable", null, take: 25, CancellationToken.None);
        var second = await repo.SearchForFlaggingAsync("flaggable", null, take: 25, CancellationToken.None);

        Assert.Equal(25, first.Count);
        Assert.Equal(first.Select(e => e.Id), second.Select(e => e.Id)); // deterministic, not order-flaky

        var allIds = await db.TimesheetEntries.Select(e => e.Id).OrderByDescending(id => id).ToListAsync();
        var expectedTop25 = allIds.Take(25).ToList(); // the 25 most recently created rows
        Assert.Equal(expectedTop25, first.Select(e => e.Id).ToList());
    }
}
