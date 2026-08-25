using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeSheet.Domain.Entities;

namespace TimeSheet.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.EntraObjectId).HasMaxLength(100);
        builder.Property(u => u.PasswordHash).HasMaxLength(500);
        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);

        // Nullable unique index - SQLite (like most databases) treats each NULL as distinct, so any number of
        // local (EntraObjectId == null) accounts can coexist with any number of SSO accounts.
        builder.HasIndex(u => u.EntraObjectId).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Ignore(u => u.IsLocalAccount);

        builder.HasMany(u => u.Assignments)
            .WithOne(a => a.User)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
