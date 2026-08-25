using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeSheet.Domain.Entities;

namespace TimeSheet.Infrastructure.Data.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.AccountCode).HasMaxLength(50).IsRequired();
        builder.Property(c => c.ReportingCurrencyCode).HasMaxLength(3).IsRequired();
        builder.HasIndex(c => c.AccountCode).IsUnique();

        builder.HasMany(c => c.Projects)
            .WithOne(p => p.Client)
            .HasForeignKey(p => p.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.AccountManagers)
            .WithOne(am => am.Client)
            .HasForeignKey(am => am.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
