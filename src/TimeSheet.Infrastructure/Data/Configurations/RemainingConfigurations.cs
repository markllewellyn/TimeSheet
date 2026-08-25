using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeSheet.Domain.Entities;

namespace TimeSheet.Infrastructure.Data.Configurations;

public class ClientAccountManagerConfiguration : IEntityTypeConfiguration<ClientAccountManager>
{
    public void Configure(EntityTypeBuilder<ClientAccountManager> builder)
    {
        builder.HasIndex(am => new { am.ClientId, am.UserId }).IsUnique();
        builder.HasOne(am => am.User).WithMany().HasForeignKey(am => am.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.Property(a => a.FileName).HasMaxLength(260).IsRequired();
        builder.Property(a => a.StorageKey).HasMaxLength(1000).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(200).IsRequired();
    }
}

public class CurrencyRateConfiguration : IEntityTypeConfiguration<CurrencyRate>
{
    public void Configure(EntityTypeBuilder<CurrencyRate> builder)
    {
        builder.Property(r => r.BaseCurrency).HasMaxLength(3).IsRequired();
        builder.Property(r => r.QuoteCurrency).HasMaxLength(3).IsRequired();
        builder.Property(r => r.Rate).HasPrecision(18, 8);
        builder.Property(r => r.Source).HasMaxLength(50);

        builder.HasIndex(r => new { r.BaseCurrency, r.QuoteCurrency, r.RateDate }).IsUnique();
    }
}

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.Property(i => i.ReportingCurrency).HasMaxLength(3).IsRequired();
        builder.Property(i => i.InvoiceNumber).HasMaxLength(50);
        builder.Property(i => i.TotalAmount).HasPrecision(18, 2);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);

        // Unique invoice number per client, scoped to Finalized invoices only (Drafts have no number yet).
        builder.HasIndex(i => new { i.ClientId, i.InvoiceNumber })
            .IsUnique()
            .HasFilter("\"Status\" = 'Finalized'");

        builder.HasMany(i => i.LineItems)
            .WithOne(li => li.Invoice)
            .HasForeignKey(li => li.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.Property(li => li.Description).HasMaxLength(500).IsRequired();
        builder.Property(li => li.Hours).HasPrecision(9, 2);
        builder.Property(li => li.Amount).HasPrecision(18, 2);
        builder.Property(li => li.Type).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(li => li.Project).WithMany().HasForeignKey(li => li.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(n => n.Message).HasMaxLength(1000).IsRequired();

        builder.HasIndex(n => new { n.RecipientUserId, n.IsRead });
        builder.HasOne(n => n.RecipientUser).WithMany().HasForeignKey(n => n.RecipientUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class EscalationConfiguration : IEntityTypeConfiguration<Escalation>
{
    public void Configure(EntityTypeBuilder<Escalation> builder)
    {
        builder.Property(e => e.Reason).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Decision).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.BudgetLimitAtTimeOfEntry).HasPrecision(18, 2);
        builder.Property(e => e.CumulativeValueAtTimeOfEntry).HasPrecision(18, 2);
        builder.Property(e => e.DecisionNotes).HasMaxLength(1000);

        builder.HasIndex(e => e.Decision);
        builder.HasOne(e => e.TimesheetEntry).WithMany().HasForeignKey(e => e.TimesheetEntryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Project).WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProjectHealthAssessmentConfiguration : IEntityTypeConfiguration<ProjectHealthAssessment>
{
    public void Configure(EntityTypeBuilder<ProjectHealthAssessment> builder)
    {
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Summary).HasMaxLength(2000).IsRequired();
        builder.Property(a => a.AiModelUsed).HasMaxLength(100).IsRequired();
        builder.Property(a => a.PercentBudgetConsumed).HasPrecision(6, 2);
        builder.Property(a => a.PercentTimeElapsed).HasPrecision(6, 2);

        builder.HasIndex(a => new { a.ProjectId, a.AssessedAtUtc });
        builder.HasOne(a => a.Project).WithMany().HasForeignKey(a => a.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AppSettingsConfiguration : IEntityTypeConfiguration<AppSettings>
{
    public void Configure(EntityTypeBuilder<AppSettings> builder)
    {
        builder.Property(s => s.BaseReportingCurrency).HasMaxLength(3).IsRequired();
        builder.HasData(new AppSettings { Id = 1, BaseReportingCurrency = "GBP", DefaultInvoiceMonthEndDay = 31 });
    }
}
