using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeSheet.Domain.Entities;

namespace TimeSheet.Infrastructure.Data.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Code).HasMaxLength(50).IsRequired();
        builder.Property(p => p.CurrencyOverride).HasMaxLength(3);
        builder.Property(p => p.BudgetHours).HasPrecision(18, 2);
        builder.Property(p => p.FixedFeeAmount).HasPrecision(18, 2);

        // Code is unique per-client, not globally.
        builder.HasIndex(p => new { p.ClientId, p.Code }).IsUnique();

        builder.HasMany(p => p.Rates)
            .WithOne(r => r.Project)
            .HasForeignKey(r => r.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Assignments)
            .WithOne(a => a.Project)
            .HasForeignKey(a => a.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // LatestHealthAssessmentId is a plain denormalized pointer (no navigation property), so EF maps it
        // as an ordinary nullable column with no inferred FK/cascade relationship to ProjectHealthAssessment.
    }
}
