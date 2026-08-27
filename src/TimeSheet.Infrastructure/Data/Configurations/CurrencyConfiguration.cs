using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeSheet.Domain.Entities;

namespace TimeSheet.Infrastructure.Data.Configurations;

/// <summary>Maps onto the legacy [Currency] table - see Currency.cs.</summary>
public class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.ToTable("Currency");
        builder.Property(c => c.Id).HasColumnName("PK_Currency");
        builder.Property(c => c.CurrencyName).HasColumnName("CurrencyName").HasMaxLength(100).IsRequired();
        builder.Property(c => c.CurrencyCode).HasColumnName("CurrencyCode").HasMaxLength(3).IsRequired();
        builder.Property(c => c.ExchangeRate).HasColumnName("ExchangeRate").HasPrecision(18, 3);
        builder.Property(c => c.IsActive).HasColumnName("IsActive");
        builder.Property(c => c.LastUpdated).HasColumnName("LastUpdated");
    }
}

/// <summary>Maps onto the legacy [CurrencyExchangeHistory] table - see CurrencyExchangeHistory.cs.</summary>
public class CurrencyExchangeHistoryConfiguration : IEntityTypeConfiguration<CurrencyExchangeHistory>
{
    public void Configure(EntityTypeBuilder<CurrencyExchangeHistory> builder)
    {
        builder.ToTable("CurrencyExchangeHistory");
        builder.Property(h => h.Id).HasColumnName("PK_History");
        builder.Property(h => h.CurrencyId).HasColumnName("PK_Currency").IsRequired();
        builder.Property(h => h.OldExchangeRate).HasColumnName("OldExchangeRate").HasPrecision(18, 3).IsRequired();
        builder.Property(h => h.NewExchangeRate).HasColumnName("NewExchangeRate").HasPrecision(18, 3).IsRequired();
        builder.Property(h => h.ChangeDate).HasColumnName("ChangeDate").IsRequired();

        builder.HasOne(h => h.Currency).WithMany().HasForeignKey(h => h.CurrencyId).OnDelete(DeleteBehavior.Cascade);
    }
}
