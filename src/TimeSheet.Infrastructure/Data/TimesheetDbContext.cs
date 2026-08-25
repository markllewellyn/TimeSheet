using Microsoft.EntityFrameworkCore;
using TimeSheet.Domain.Entities;
using TimeSheet.Domain.Repositories;

namespace TimeSheet.Infrastructure.Data;

public class TimesheetDbContext(DbContextOptions<TimesheetDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientAccountManager> ClientAccountManagers => Set<ClientAccountManager>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectRate> ProjectRates => Set<ProjectRate>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ProjectAssignment> ProjectAssignments => Set<ProjectAssignment>();
    public DbSet<TimesheetEntry> TimesheetEntries => Set<TimesheetEntry>();
    public DbSet<ExpenseEntry> ExpenseEntries => Set<ExpenseEntry>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<CurrencyRate> CurrencyRates => Set<CurrencyRate>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Escalation> Escalations => Set<Escalation>();
    public DbSet<ProjectHealthAssessment> ProjectHealthAssessments => Set<ProjectHealthAssessment>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TimesheetDbContext).Assembly);
    }

    // DbContext.SaveChangesAsync(CancellationToken) already matches IUnitOfWork.SaveChangesAsync's signature -
    // no explicit implementation needed.
}
