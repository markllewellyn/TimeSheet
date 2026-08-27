using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeSheet.Domain.Entities;

namespace TimeSheet.Infrastructure.Data.Configurations;

/// <summary>Maps onto the legacy [RecordedTimesarc] table - see RecordedTimeArchive.cs. Non-identity PK, since
/// archived rows preserve their original RecordedTimes id rather than getting a new one.</summary>
public class RecordedTimeArchiveConfiguration : IEntityTypeConfiguration<RecordedTimeArchive>
{
    public void Configure(EntityTypeBuilder<RecordedTimeArchive> builder)
    {
        builder.ToTable("RecordedTimesarc");
        builder.Property(e => e.Id).HasColumnName("PK_RecordedTimes").ValueGeneratedNever();
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
        builder.Property(e => e.ApprovedByName).HasColumnName("ApprovedByName").HasMaxLength(100);
        builder.Property(e => e.SentByName).HasColumnName("SentByName").HasMaxLength(100);
        builder.Property(e => e.PostingBatch).HasColumnName("PostingBatch").HasMaxLength(20);
        builder.Property(e => e.ExpensesValue).HasColumnName("ExpensesValue").HasPrecision(18, 2);
    }
}
