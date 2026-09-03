using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeSheet.Domain.Entities;

namespace TimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Maps onto the legacy [Projects] table (Id/ClientId/Name/IsActive/CanInvoice), extended directly with the
/// Code/Description/PaymentModel/budget/date columns the legacy table has no room for - see ClientConfiguration
/// for why this extends the table directly rather than using a separate split table. See Project.cs.
/// </summary>
public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.Property(p => p.Id).HasColumnName("PK_Projects");
        builder.Property(p => p.ClientId).HasColumnName("PK_Customers");
        builder.Property(p => p.Name).HasColumnName("ProjectName").HasMaxLength(50).IsRequired();
        builder.Property(p => p.IsActive).HasColumnName("Active").IsRequired();
        builder.Property(p => p.CanInvoice).HasColumnName("CanInvoice");

        builder.Property(p => p.Code).HasMaxLength(50).IsRequired();
        builder.Property(p => p.CurrencyOverride).HasMaxLength(3);
        builder.Property(p => p.BudgetHours).HasPrecision(18, 2);
        builder.Property(p => p.FixedFeeAmount).HasPrecision(18, 2);
        builder.Property(p => p.ProjectType).HasConversion<string>().HasMaxLength(20);

        // Code is unique per-client, not globally.
        builder.HasIndex(p => new { p.ClientId, p.Code }).IsUnique();

        builder.HasMany(p => p.Assignments)
            .WithOne(a => a.Project)
            .HasForeignKey(a => a.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Attachments)
            .WithOne(a => a.Project)
            .HasForeignKey(a => a.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not Cascade: removing a user who happens to be a PM must not silently delete the project -
        // the admin has to reassign the PM first (same reasoning as every other staff FK on this entity).
        builder.HasOne(p => p.ProjectManager)
            .WithMany()
            .HasForeignKey(p => p.ProjectManagerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
