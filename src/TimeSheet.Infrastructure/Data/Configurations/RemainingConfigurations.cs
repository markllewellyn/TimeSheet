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

public class ProjectAttachmentConfiguration : IEntityTypeConfiguration<ProjectAttachment>
{
    public void Configure(EntityTypeBuilder<ProjectAttachment> builder)
    {
        builder.Property(a => a.FileName).HasMaxLength(260).IsRequired();
        builder.Property(a => a.StorageKey).HasMaxLength(1000).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(200).IsRequired();

        // Restrict, not Cascade - matches every other staff FK on Project (e.g. ProjectManager): removing a
        // user who happens to have uploaded a document must not silently delete that document.
        builder.HasOne(a => a.UploadedBy).WithMany().HasForeignKey(a => a.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
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
        builder.Property(i => i.ExchangeRate).HasPrecision(18, 8);
        builder.Property(i => i.InvoiceNumber).HasMaxLength(50);
        builder.Property(i => i.PdfStorageKey).HasMaxLength(1000);
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

public class EntryFlagConfiguration : IEntityTypeConfiguration<EntryFlag>
{
    public void Configure(EntityTypeBuilder<EntryFlag> builder)
    {
        builder.Property(e => e.Reason).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.BudgetLimitAtTimeOfEntry).HasPrecision(18, 2);
        builder.Property(e => e.CumulativeValueAtTimeOfEntry).HasPrecision(18, 2);
        builder.Property(e => e.RaisedNotes).HasMaxLength(1000);
        builder.Property(e => e.ClearedNotes).HasMaxLength(1000);

        builder.HasIndex(e => e.IsCleared);
        builder.HasOne(e => e.TimesheetEntry).WithMany().HasForeignKey(e => e.TimesheetEntryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Project).WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(a => a.UserDisplayName).HasMaxLength(200).IsRequired();
        builder.Property(a => a.ImpersonatedUserDisplayName).HasMaxLength(200);
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Details).HasMaxLength(1000);

        // No FK constraints - deliberate, the audit trail must survive the user/entry it describes being deleted.
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.ImpersonatedUserId);
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(r => r.Name).IsUnique();
    }
}

public class RateCardConfiguration : IEntityTypeConfiguration<RateCard>
{
    public void Configure(EntityTypeBuilder<RateCard> builder)
    {
        builder.Property(rc => rc.Rate).HasPrecision(18, 2).IsRequired();

        // A "change" is a new dated row, never a mutation - see RateCard.cs.
        builder.HasIndex(rc => new { rc.RoleId, rc.StaffId, rc.ClientId, rc.ProjectId, rc.EffectiveFrom }).IsUnique();

        // Enforces RateCard's scope shape at the DB layer, not just in the API - this table is billing-load-
        // bearing, a malformed row silently breaks invoices/payroll, so it's worth the extra rigor other
        // nullable-FK-pair tables in this codebase (e.g. User.EntraObjectId/PasswordHash) don't have.
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_RateCard_ScopeXor", "(\"RoleId\" IS NOT NULL AND \"StaffId\" IS NULL) OR (\"RoleId\" IS NULL AND \"StaffId\" IS NOT NULL)");
            t.HasCheckConstraint("CK_RateCard_NotBothClientProject", "\"ClientId\" IS NULL OR \"ProjectId\" IS NULL");
            t.HasCheckConstraint("CK_RateCard_StaffRequiresScope", "\"StaffId\" IS NULL OR \"ClientId\" IS NOT NULL OR \"ProjectId\" IS NOT NULL");
        });

        builder.HasOne(rc => rc.Role).WithMany().HasForeignKey(rc => rc.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(rc => rc.Staff).WithMany().HasForeignKey(rc => rc.StaffId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(rc => rc.Client).WithMany().HasForeignKey(rc => rc.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(rc => rc.Project).WithMany().HasForeignKey(rc => rc.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AppSettingsConfiguration : IEntityTypeConfiguration<AppSettings>
{
    public void Configure(EntityTypeBuilder<AppSettings> builder)
    {
        builder.Property(s => s.BaseReportingCurrency).HasMaxLength(3).IsRequired();
        builder.HasData(new AppSettings
        {
            Id = 1,
            BaseReportingCurrency = "GBP",
            DefaultInvoiceMonthEndDay = 31,
            ProjectBudgetWarningThresholdPercent = 50,
            ProjectBudgetAlertThresholdPercent = 75,
        });
    }
}
