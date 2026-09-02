using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;

namespace TimeSheet.Infrastructure.Data.Configurations;

/// <summary>Maps onto the legacy [RecordedTimes] table - see TimesheetEntry.cs.</summary>
public class TimesheetEntryConfiguration : IEntityTypeConfiguration<TimesheetEntry>
{
    public void Configure(EntityTypeBuilder<TimesheetEntry> builder)
    {
        builder.ToTable("RecordedTimes");
        builder.Property(e => e.Id).HasColumnName("PK_RecordedTimes");
        builder.Property(e => e.UserId).HasColumnName("PK_Staff");
        builder.Property(e => e.ClientId).HasColumnName("PK_Customers");
        builder.Property(e => e.ProjectId).HasColumnName("PK_Projects");
        builder.Property(e => e.CreatedUtc).HasColumnName("CreatedDate");
        builder.Property(e => e.Date).HasColumnName("TaskDate");
        builder.Property(e => e.Description).HasColumnName("Description").HasMaxLength(100).IsRequired();
        builder.Property(e => e.ToPayroll).HasColumnName("ToPayroll").HasPrecision(7, 2);
        builder.Property(e => e.ToCompany).HasColumnName("ToCompany").HasPrecision(7, 2);
        builder.Property(e => e.WorkHours).HasColumnName("WorkHours").HasPrecision(4, 1);
        builder.Property(e => e.OutOfHoursHours).HasColumnName("OutOfHours").HasPrecision(4, 1);
        builder.Property(e => e.ApprovedPayroll).HasColumnName("ApprovedPayroll");
        builder.Property(e => e.ApprovedByStaffId).HasColumnName("ApprovedByStaffId");
        builder.Property(e => e.ApprovedByName).HasColumnName("ApprovedByName").HasMaxLength(100);
        builder.Property(e => e.DateApprovedPayroll).HasColumnName("DateApprovedPayroll");
        builder.Property(e => e.SentToPayroll).HasColumnName("SentToPayroll");
        builder.Property(e => e.SentByStaffId).HasColumnName("SentByStaffId");
        builder.Property(e => e.SentByName).HasColumnName("SentByName").HasMaxLength(100);
        builder.Property(e => e.DateSentToPayroll).HasColumnName("DateSentToPayroll");
        builder.Property(e => e.PostingBatch).HasColumnName("PostingBatch").HasMaxLength(20);
        builder.Property(e => e.ExpensesValue).HasColumnName("ExpensesValue").HasPrecision(18, 2);
        builder.Property(e => e.ResolvedCustomerRate).HasPrecision(18, 2);
        builder.Property(e => e.ResolvedHourlyCost).HasPrecision(18, 2);
        builder.Property(e => e.ResolvedOutOfHoursCost).HasPrecision(18, 2);
        builder.Property(e => e.Tier).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.BillingPeriodChoice).HasConversion<string>().HasMaxLength(10).HasDefaultValue(BillingPeriodChoice.Current);

        // Covering indexes for the reporting aggregation queries (Project,Date) and (User,Date).
        builder.HasIndex(e => new { e.ProjectId, e.Date });
        builder.HasIndex(e => new { e.UserId, e.Date });
        builder.HasIndex(e => new { e.ClientId, e.Date });

        builder.HasOne(e => e.Client).WithMany().HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Project).WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.RateCard).WithMany().HasForeignKey(e => e.RateCardId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.StaffCost).WithMany().HasForeignKey(e => e.StaffCostId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.EntryType).WithMany().HasForeignKey(e => e.EntryTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Attachments)
            .WithOne(a => a.TimesheetEntry)
            .HasForeignKey(a => a.TimesheetEntryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
