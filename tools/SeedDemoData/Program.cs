using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;
using TimeSheet.Infrastructure.Data;
using TimeSheet.Infrastructure.Services;

// Run from this project's own directory (`dotnet run` sets the working directory here), pointed at the same
// SQLite file the running Functions host uses - stop `func start` before running this, since SQLite doesn't
// like two writers.
var dbPath = args.Length > 0 ? args[0] : "../../src/TimeSheet.Api/bin/output/timesheet.db";

var options = new DbContextOptionsBuilder<TimesheetDbContext>()
    .UseSqlite($"Data Source={dbPath}")
    .Options;

await using var db = new TimesheetDbContext(options);

if (args.Length >= 3 && args[1] == "--verify-login")
{
    var testEmail = args[2];
    var testPassword = args.Length >= 4 ? args[3] : "";
    var testUser = await db.Users.FirstOrDefaultAsync(u => u.Email == testEmail);
    if (testUser is null)
    {
        Console.WriteLine($"No user found with Email == '{testEmail}' (exact match).");
        var allEmails = await db.Users.Select(u => u.Email).ToListAsync();
        Console.WriteLine($"Existing emails: {string.Join(", ", allEmails)}");
        return;
    }

    Console.WriteLine($"Found user Id={testUser.Id} Email={testUser.Email} IsActive={testUser.IsActive} HasPasswordHash={testUser.PasswordHash is not null}");
    if (testUser.PasswordHash is null)
    {
        Console.WriteLine("This account has no PasswordHash (it's an SSO-only account) - local login can never work for it.");
        return;
    }

    var verifyResult = new Pbkdf2PasswordHasher().Verify(testPassword, testUser.PasswordHash);
    Console.WriteLine($"Password '{testPassword}' verifies against stored hash: {verifyResult}");
    return;
}

if (args.Length >= 2 && args[1] == "--counts")
{
    Console.WriteLine($"Clients={await db.Clients.CountAsync()}, Projects={await db.Projects.CountAsync()}, " +
        $"Users={await db.Users.CountAsync()}, StaffCosts={await db.StaffCosts.CountAsync()}, " +
        $"Assignments={await db.StaffProjects.CountAsync()}, Currencies={await db.Currencies.CountAsync()}");
    foreach (var c in await db.Clients.ToListAsync())
        Console.WriteLine($"  Client Id={c.Id} Name={c.Name} AccountCode={c.AccountCode} IsActive={c.IsActive}");
    return;
}

if (args.Length >= 4 && args[1] == "--set-password")
{
    var targetEmail = args[2];
    var newPassword = args[3];
    var targetUser = await db.Users.FirstOrDefaultAsync(u => u.Email == targetEmail)
        ?? throw new InvalidOperationException($"No user found with Email == '{targetEmail}'.");

    targetUser.PasswordHash = new Pbkdf2PasswordHasher().Hash(newPassword);
    targetUser.ModifiedUtc = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync();
    Console.WriteLine($"Password set for {targetUser.Email} (Id={targetUser.Id}, Role={targetUser.Role}).");
    return;
}

if (await db.Clients.AnyAsync(c => c.AccountCode == "NORTH01"))
{
    Console.WriteLine("Demo data already seeded (found client NORTH01) - skipping to avoid duplicating it.");
    return;
}

var allUsers = await db.Users.ToListAsync();
Console.WriteLine($"Found {allUsers.Count} existing user(s):");
foreach (var u in allUsers) Console.WriteLine($"  Id={u.Id} Email={u.Email} Role={u.Role} IsActive={u.IsActive} CreatedUtc={u.CreatedUtc:O} EntraObjectId={u.EntraObjectId} HasPasswordHash={u.PasswordHash is not null}");

var admin = allUsers.FirstOrDefault(u => u.Role == UserRole.Admin);
if (admin is null && allUsers.Count == 1)
{
    // Single-user demo environment with no Admin left (e.g. self-demoted via the Users page's role toggle) -
    // safe to just restore Admin on the one account that exists, rather than hard-failing the seed.
    admin = allUsers[0];
    admin.Role = UserRole.Admin;
    Console.WriteLine($"Restoring Admin role on {admin.Email} (was {allUsers[0].Role}).");
    await db.SaveChangesAsync();
}

if (admin is null)
{
    Console.WriteLine("No Admin user found - bootstrap the first Admin account via the app before seeding demo data.");
    return;
}

// Admin needs a PayrollNumber too, in case it was bootstrapped before this column existed on an older DB.
if (string.IsNullOrEmpty(admin.PayrollNumber))
{
    admin.PayrollNumber = $"STF{admin.Id:D4}";
    await db.SaveChangesAsync();
}

var hasher = new Pbkdf2PasswordHasher();
var now = DateTimeOffset.UtcNow;

// Currency rows are the legacy schema's master list - seeded first since Clients reference them by Id.
var gbp = new Currency { CurrencyName = "British Pound", CurrencyCode = "GBP", ExchangeRate = 1.0m, IsActive = true, LastUpdated = now };
var eur = new Currency { CurrencyName = "Euro", CurrencyCode = "EUR", ExchangeRate = 1.17m, IsActive = true, LastUpdated = now };
var usd = new Currency { CurrencyName = "US Dollar", CurrencyCode = "USD", ExchangeRate = 1.27m, IsActive = true, LastUpdated = now };
db.Currencies.AddRange(gbp, eur, usd);
await db.SaveChangesAsync();

Client MakeClient(string name, string accountCode, Currency currency, DateOnly startDate, string contactName, string contactEmail) => new()
{
    Name = name,
    AccountCode = accountCode,
    StartDate = startDate,
    CurrencyId = currency.Id,
    BillingCity = "London",
    BillingCountryCode = "GB",
    PrimaryContactName = contactName,
    PrimaryContactEmail = contactEmail,
    IsActive = true,
    CreatedUtc = now,
    CreatedByUserId = admin.Id,
};

var northwind = MakeClient("Northwind Logistics Ltd", "NORTH01", gbp, new DateOnly(2026, 1, 1), "Helen Marsh", "helen.marsh@northwindlogistics.co.uk");
var acme = MakeClient("Acme Manufacturing GmbH", "ACME01", eur, new DateOnly(2026, 1, 1), "Klaus Richter", "klaus.richter@acme-mfg.de");
var brightpath = MakeClient("BrightPath Retail Group", "BRIGHT01", usd, new DateOnly(2026, 1, 1), "Dana Alvarez", "dana.alvarez@brightpathretail.com");

db.Clients.AddRange(northwind, acme, brightpath);
await db.SaveChangesAsync();

User MakeUser(string displayName, string email, string jobTitle, decimal hourlyCost) => new()
{
    DisplayName = displayName,
    Email = email,
    PasswordHash = hasher.Hash("Demo123!"),
    Role = UserRole.User,
    JobTitle = jobTitle,
    HourlyCost = hourlyCost,
    PayrollNumber = "PENDING", // replaced below once Id is assigned
    IsActive = true,
    CreatedUtc = now,
};

var sarah = MakeUser("Sarah Chen", "sarah.chen@svgit.co.uk", "Senior Consultant", 45m);
var james = MakeUser("James O'Brien", "james.obrien@svgit.co.uk", "Consultant", 38m);
var priya = MakeUser("Priya Patel", "priya.patel@svgit.co.uk", "Consultant", 38m);

db.Users.AddRange(sarah, james, priya);
await db.SaveChangesAsync();

foreach (var u in new[] { sarah, james, priya }) u.PayrollNumber = $"STF{u.Id:D4}";
await db.SaveChangesAsync();

Project MakeProject(
    Client client, string name, string code, PaymentModel model, DateOnly start, DateOnly? end,
    decimal? budgetHours, decimal? fixedFee)
{
    return new Project
    {
        ClientId = client.Id,
        Name = name,
        Code = code,
        PaymentModel = model,
        StartDate = start,
        EndDate = end,
        BudgetHours = budgetHours,
        FixedFeeAmount = fixedFee,
        CanInvoice = true,
        IsActive = true,
        CreatedUtc = now,
        CreatedByUserId = admin.Id,
    };
}

var warehouseOps = MakeProject(northwind, "Warehouse Ops Optimisation", "NORTH01-WOO", PaymentModel.TimeAndMaterials,
    new DateOnly(2026, 6, 1), null, 400m, null);
var wmsSupport = MakeProject(northwind, "WMS Support Retainer", "NORTH01-SUP", PaymentModel.TimeAndMaterials,
    new DateOnly(2026, 1, 1), null, null, null);
var erpMigration = MakeProject(acme, "ERP Migration Phase 2", "ACME01-ERP2", PaymentModel.FixedProjectCost,
    new DateOnly(2026, 3, 1), new DateOnly(2026, 11, 30), 600m, 85000m);
var dataQualityAudit = MakeProject(acme, "Data Quality Audit", "ACME01-DQA", PaymentModel.TimeAndMaterials,
    new DateOnly(2026, 7, 1), null, 120m, null);
var posRollout = MakeProject(brightpath, "POS Rollout", "BRIGHT01-POS", PaymentModel.FixedProjectCost,
    new DateOnly(2026, 5, 1), new DateOnly(2026, 10, 31), 300m, 45000m);
var loyaltyApp = MakeProject(brightpath, "Loyalty App Discovery", "BRIGHT01-LOY", PaymentModel.TimeAndMaterials,
    new DateOnly(2026, 8, 1), null, 80m, null);

db.Projects.AddRange(warehouseOps, wmsSupport, erpMigration, dataQualityAudit, posRollout, loyaltyApp);
await db.SaveChangesAsync();

// StaffCosts are keyed by (Staff, Client), not (Staff, Project) - one row covers every project a person works
// on for that client. Must exist before any StaffProject assignment for the same pair.
StaffCost MakeStaffCost(User staff, Client client, decimal customerRate, decimal outOfHoursCost) => new()
{
    StaffId = staff.Id,
    ClientId = client.Id,
    CustomerRate = customerRate,
    OutOfHoursCost = outOfHoursCost,
};

var adminNorthwind = MakeStaffCost(admin, northwind, 120m, 180m);
var adminAcme = MakeStaffCost(admin, acme, 140m, 210m);
var sarahAcme = MakeStaffCost(sarah, acme, 115m, 170m);
var jamesNorthwind = MakeStaffCost(james, northwind, 110m, 165m);
var priyaBrightpath = MakeStaffCost(priya, brightpath, 130m, 195m);

db.StaffCosts.AddRange(adminNorthwind, adminAcme, sarahAcme, jamesNorthwind, priyaBrightpath);
await db.SaveChangesAsync();

StaffProject Assign(StaffCost staffCost, Project project, decimal weeklyHours, DateOnly start) => new()
{
    StaffCostId = staffCost.Id,
    ProjectId = project.Id,
    IsActive = true,
    StartDate = start,
    AllocatedHoursPerWeek = weeklyHours,
    CreatedUtc = now,
    CreatedByUserId = admin.Id,
};

var assignments = new List<StaffProject>
{
    Assign(adminNorthwind, warehouseOps, 8m, warehouseOps.StartDate),
    Assign(adminAcme, erpMigration, 4m, erpMigration.StartDate),
    Assign(sarahAcme, erpMigration, 30m, erpMigration.StartDate),
    Assign(sarahAcme, dataQualityAudit, 10m, dataQualityAudit.StartDate),
    Assign(jamesNorthwind, warehouseOps, 25m, warehouseOps.StartDate),
    Assign(jamesNorthwind, wmsSupport, 10m, wmsSupport.StartDate),
    Assign(priyaBrightpath, posRollout, 20m, posRollout.StartDate),
    Assign(priyaBrightpath, loyaltyApp, 15m, loyaltyApp.StartDate),
};
db.StaffProjects.AddRange(assignments);
await db.SaveChangesAsync();

var descriptions = new[]
{
    "Stakeholder workshop", "Requirements analysis", "Configuration & build", "Testing & defect fixes",
    "Status reporting", "Data migration prep", "UAT support", "Documentation",
};

var today = DateOnly.FromDateTime(DateTime.UtcNow);
var entries = new List<TimesheetEntry>();
var rng = new Random(42);

void SeedWeeksOfEntries(Project project, User user, decimal weeklyHours, int weeks)
{
    var dailyHours = Math.Round(weeklyHours / 5m, 2);
    var monday = today.AddDays(-(int)today.DayOfWeek + 1); // this week's Monday (DayOfWeek: Sun=0..Sat=6)

    for (var w = 0; w < weeks; w++)
    {
        var weekStart = monday.AddDays(-7 * w);
        for (var d = 0; d < 5; d++)
        {
            var date = weekStart.AddDays(d);
            if (date > today || date < project.StartDate) continue;

            entries.Add(new TimesheetEntry
            {
                UserId = user.Id,
                ClientId = project.ClientId,
                ProjectId = project.Id,
                Date = date,
                WorkHours = dailyHours,
                OutOfHoursHours = 0,
                Description = descriptions[rng.Next(descriptions.Length)],
                Status = TimesheetEntryStatus.Normal,
                CreatedUtc = now,
            });
        }
    }
}

SeedWeeksOfEntries(warehouseOps, admin, 8m, 3);
SeedWeeksOfEntries(erpMigration, admin, 4m, 3);
SeedWeeksOfEntries(erpMigration, sarah, 30m, 3);
SeedWeeksOfEntries(dataQualityAudit, sarah, 10m, 3);
SeedWeeksOfEntries(warehouseOps, james, 25m, 3);
SeedWeeksOfEntries(wmsSupport, james, 10m, 3);
SeedWeeksOfEntries(posRollout, priya, 20m, 3);
SeedWeeksOfEntries(loyaltyApp, priya, 15m, 3);

db.TimesheetEntries.AddRange(entries);

db.ExpenseEntries.AddRange(
    new ExpenseEntry
    {
        UserId = james.Id,
        ProjectId = warehouseOps.Id,
        Date = today.AddDays(-3),
        Amount = 84.50m,
        Currency = "GBP",
        Description = "Site visit - mileage & parking",
        IsBillable = true,
        CreatedUtc = now,
    },
    new ExpenseEntry
    {
        UserId = sarah.Id,
        ProjectId = erpMigration.Id,
        Date = today.AddDays(-5),
        Amount = 42.10m,
        Currency = "EUR",
        Description = "Client site lunch (workshop day)",
        IsBillable = true,
        CreatedUtc = now,
    });

await db.SaveChangesAsync();

Console.WriteLine($"Seeded {await db.Currencies.CountAsync()} currencies, {await db.Clients.CountAsync()} clients, " +
    $"{await db.Projects.CountAsync()} projects, {await db.Users.CountAsync()} users, " +
    $"{await db.StaffCosts.CountAsync()} staff costs, {await db.StaffProjects.CountAsync()} assignments, " +
    $"{await db.TimesheetEntries.CountAsync()} timesheet entries, {await db.ExpenseEntries.CountAsync()} expense entries.");
Console.WriteLine("Demo user login (all 3): password \"Demo123!\"");
Console.WriteLine($"  {sarah.Email}");
Console.WriteLine($"  {james.Email}");
Console.WriteLine($"  {priya.Email}");
