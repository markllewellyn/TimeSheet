using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeSheet.Domain.Entities;

namespace TimeSheet.Infrastructure.Data.Configurations;

/// <summary>Genuinely new shape (no legacy mirroring) - see StaffCost.cs.</summary>
public class StaffCostConfiguration : IEntityTypeConfiguration<StaffCost>
{
    public void Configure(EntityTypeBuilder<StaffCost> builder)
    {
        builder.Property(sc => sc.HourlyCost).HasPrecision(18, 2).IsRequired();
        builder.Property(sc => sc.OutOfHoursCost).HasPrecision(18, 2).IsRequired();

        // A "change" is a new dated row, never a mutation - see StaffCost.cs.
        builder.HasIndex(sc => new { sc.StaffId, sc.EffectiveFrom }).IsUnique();

        builder.HasOne(sc => sc.Staff).WithMany().HasForeignKey(sc => sc.StaffId).OnDelete(DeleteBehavior.Restrict);
    }
}
