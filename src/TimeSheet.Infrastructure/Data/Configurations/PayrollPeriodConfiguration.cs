using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeSheet.Domain.Entities;

namespace TimeSheet.Infrastructure.Data.Configurations;

public class PayrollPeriodConfiguration : IEntityTypeConfiguration<PayrollPeriod>
{
    public void Configure(EntityTypeBuilder<PayrollPeriod> builder)
    {
        builder.Property(p => p.TotalOutOfHoursHours).HasPrecision(18, 2);
        builder.Property(p => p.TotalOutOfHoursPay).HasPrecision(18, 2);

        // One aggregation per calendar month - re-running the timer for an already-generated month updates
        // the existing row (ClearLines + rebuild) rather than creating a duplicate.
        builder.HasIndex(p => p.PeriodStart).IsUnique();

        builder.HasMany(p => p.Lines).WithOne(l => l.PayrollPeriod).HasForeignKey(l => l.PayrollPeriodId).OnDelete(DeleteBehavior.Cascade);
    }
}
