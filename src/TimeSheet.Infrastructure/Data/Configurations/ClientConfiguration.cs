using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;

namespace TimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Maps onto the legacy [Customers] table (Id/Name/AccountCode/StartDate/IsActive/CurrencyId), extended
/// directly with the billing/contact/notes/audit columns the legacy table has no room for - same pattern as
/// Staff.PasswordHash/EntraObjectId - rather than a separate split table, which fought EF Core's exact
/// SplitToTable API shape in this environment for no real benefit over just adding the columns. See Client.cs.
/// </summary>
public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Customers");
        builder.Property(c => c.Id).HasColumnName("PK_Customers");
        builder.Property(c => c.Name).HasColumnName("CustomerName").HasMaxLength(40).IsRequired();
        builder.Property(c => c.AccountCode).HasColumnName("Account").HasMaxLength(50).IsRequired();
        builder.Property(c => c.StartDate).HasColumnName("StartDate").IsRequired();
        builder.Property(c => c.IsActive).HasColumnName("Active").IsRequired();
        builder.Property(c => c.CurrencyId).HasColumnName("PK_Currency");
        builder.Ignore(c => c.ReportingCurrencyCode);

        builder.Property(c => c.BillingAddressLine1).HasMaxLength(200);
        builder.Property(c => c.BillingAddressLine2).HasMaxLength(200);
        builder.Property(c => c.BillingCity).HasMaxLength(100);
        builder.Property(c => c.BillingPostalCode).HasMaxLength(20);
        builder.Property(c => c.BillingCountryCode).HasMaxLength(2);
        builder.Property(c => c.PrimaryContactName).HasMaxLength(200);
        builder.Property(c => c.PrimaryContactEmail).HasMaxLength(320);
        builder.Property(c => c.PrimaryContactPhone).HasMaxLength(50);
        builder.Property(c => c.Notes).HasMaxLength(2000);

        builder.Property(c => c.BillingPeriod).HasConversion<string>().HasMaxLength(20).IsRequired().HasDefaultValue(BillingPeriod.OneOff);

        builder.HasIndex(c => c.AccountCode).IsUnique();
        builder.HasOne(c => c.Currency).WithMany().HasForeignKey(c => c.CurrencyId).OnDelete(DeleteBehavior.SetNull);

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
