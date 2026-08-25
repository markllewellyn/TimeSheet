using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeSheet.Domain.Entities;

namespace TimeSheet.Infrastructure.Data.Configurations;

public class ProjectAssignmentConfiguration : IEntityTypeConfiguration<ProjectAssignment>
{
    public void Configure(EntityTypeBuilder<ProjectAssignment> builder)
    {
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.AllocatedHoursPerWeek).HasPrecision(9, 2);

        // At most one Active/Paused assignment per (project,user) at a time; historical Ended rows are preserved.
        builder.HasIndex(a => new { a.ProjectId, a.UserId })
            .IsUnique()
            .HasFilter("\"Status\" <> 'Ended'");
    }
}
