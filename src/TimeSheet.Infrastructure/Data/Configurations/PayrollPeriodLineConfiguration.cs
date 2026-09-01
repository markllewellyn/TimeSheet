using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeSheet.Domain.Entities;

namespace TimeSheet.Infrastructure.Data.Configurations;

public class PayrollPeriodLineConfiguration : IEntityTypeConfiguration<PayrollPeriodLine>
{
    public void Configure(EntityTypeBuilder<PayrollPeriodLine> builder)
    {
        builder.Property(l => l.OutOfHoursHours).HasPrecision(18, 2);
        builder.Property(l => l.OutOfHoursPay).HasPrecision(18, 2);

        builder.HasIndex(l => new { l.PayrollPeriodId, l.UserId }).IsUnique();

        builder.HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
