using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeSheet.Domain.Entities;

namespace TimeSheet.Infrastructure.Data.Configurations;

public class TimesheetEntryConfiguration : IEntityTypeConfiguration<TimesheetEntry>
{
    public void Configure(EntityTypeBuilder<TimesheetEntry> builder)
    {
        builder.Property(e => e.WorkHours).HasPrecision(6, 2);
        builder.Property(e => e.OutOfHoursHours).HasPrecision(6, 2);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Description).HasMaxLength(2000);

        // Covering indexes for the reporting aggregation queries (Project,Date) and (User,Date).
        builder.HasIndex(e => new { e.ProjectId, e.Date });
        builder.HasIndex(e => new { e.UserId, e.Date });

        builder.HasOne(e => e.Project).WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Attachments)
            .WithOne(a => a.TimesheetEntry)
            .HasForeignKey(a => a.TimesheetEntryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
