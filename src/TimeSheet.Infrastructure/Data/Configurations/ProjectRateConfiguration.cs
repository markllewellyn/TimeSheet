using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeSheet.Domain.Entities;

namespace TimeSheet.Infrastructure.Data.Configurations;

public class ProjectRateConfiguration : IEntityTypeConfiguration<ProjectRate>
{
    public void Configure(EntityTypeBuilder<ProjectRate> builder)
    {
        builder.Property(r => r.BillingRatePerHour).HasPrecision(18, 4);
        builder.Property(r => r.CostRatePerHour).HasPrecision(18, 4);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Non-unique index (not a uniqueness constraint) - overlap validation is service-layer (IProjectRateRepository.HasOverlapAsync),
        // since overlap requires date-range logic a plain unique index can't express.
        builder.HasIndex(r => new { r.ProjectId, r.UserId, r.EffectiveFrom });
    }
}
