using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeSheet.Domain.Entities;

namespace TimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Maps onto the legacy [StaffProjects] table (Id/StaffId/ProjectId/IsActive), extended directly with
/// AllocatedHoursPerWeek/StartDate/EndDate/Notes, which have no legacy equivalent - see StaffProject.cs.
/// </summary>
public class StaffProjectConfiguration : IEntityTypeConfiguration<StaffProject>
{
    public void Configure(EntityTypeBuilder<StaffProject> builder)
    {
        builder.ToTable("StaffProjects");
        builder.Property(sp => sp.Id).HasColumnName("PK_StaffProjects");
        builder.Property(sp => sp.StaffId).HasColumnName("PK_Staff");
        builder.Property(sp => sp.ProjectId).HasColumnName("PK_Projects");
        builder.Property(sp => sp.IsActive).HasColumnName("Active").IsRequired();
        builder.Property(sp => sp.AllocatedHoursPerWeek).HasColumnName("AllocatedHoursPerWeek").HasPrecision(9, 2);
        builder.Property(sp => sp.Notes).HasMaxLength(1000);

        // Matches the legacy UNIQUE_PK_StaffCosts_Pk_Projects constraint's intent ("at most one active
        // assignment per person+project"), but filtered to active rows only - unlike the legacy DB, we allow
        // re-assigning someone to a project after a prior assignment there has ended (IsActive=false). Overlap
        // across active/ended rows is enforced at the application layer (IStaffProjectRepository.IsOverlappingAsync).
        builder.HasIndex(sp => new { sp.StaffId, sp.ProjectId })
            .IsUnique()
            .HasFilter("\"Active\" = 1");

        builder.HasOne(sp => sp.Staff).WithMany().HasForeignKey(sp => sp.StaffId).OnDelete(DeleteBehavior.Restrict);
    }
}
