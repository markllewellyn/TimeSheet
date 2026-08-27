using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;

namespace TimeSheet.Infrastructure.Data;

// The legacy source schema groups its tables under a SQL Server "[Timesheets]" schema. SQLite has no concept
// of schemas; EF Core's Sqlite provider silently no-ops any schema argument passed to ToTable(), so we
// deliberately omit it here rather than let the code lie about namespacing that doesn't actually exist.
public class TimesheetDbContext(DbContextOptions<TimesheetDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientAccountManager> ClientAccountManagers => Set<ClientAccountManager>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<User> Users => Set<User>();
    public DbSet<StaffCost> StaffCosts => Set<StaffCost>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RateCard> RateCards => Set<RateCard>();
    public DbSet<StaffProject> StaffProjects => Set<StaffProject>();
    public DbSet<TimesheetEntry> TimesheetEntries => Set<TimesheetEntry>();
    public DbSet<RecordedTimeArchive> RecordedTimeArchives => Set<RecordedTimeArchive>();
    public DbSet<ExpenseEntry> ExpenseEntries => Set<ExpenseEntry>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<CurrencyExchangeHistory> CurrencyExchangeHistories => Set<CurrencyExchangeHistory>();
    public DbSet<CurrencyRate> CurrencyRates => Set<CurrencyRate>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<EntryFlag> EntryFlags => Set<EntryFlag>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ProjectHealthAssessment> ProjectHealthAssessments => Set<ProjectHealthAssessment>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TimesheetDbContext).Assembly);
    }

    // DbContext.SaveChangesAsync(CancellationToken) already matches IUnitOfWork.SaveChangesAsync's signature -
    // no explicit implementation needed.
}
