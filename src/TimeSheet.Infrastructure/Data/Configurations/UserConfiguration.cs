using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeSheet.Domain;
using TimeSheet.Domain.Entities;

namespace TimeSheet.Infrastructure.Data.Configurations;

/// <summary>
/// Maps onto the legacy [Staff] table (Id/DisplayName/Email/PayrollNumber/Role/IsActive/HourlyCost, plus
/// PasswordHash/EntraObjectId and JobTitle extending it directly) - see User.cs.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Staff");
        builder.Property(u => u.Id).HasColumnName("PK_Staff");
        builder.Property(u => u.DisplayName).HasColumnName("FullName").HasMaxLength(40).IsRequired();
        builder.Property(u => u.Email).HasColumnName("Email").HasMaxLength(50).IsRequired();
        builder.Property(u => u.PayrollNumber).HasColumnName("PayrollNumber").HasMaxLength(10).IsRequired();
        builder.Property(u => u.IsActive).HasColumnName("Active").IsRequired();
        builder.Property(u => u.EntraObjectId).HasColumnName("EntraObjectId").HasMaxLength(100);
        builder.Property(u => u.PasswordHash).HasColumnName("PasswordHash").HasMaxLength(500);

        // Role is a flat two-value enum (Admin|User) - the legacy schema only has a single Admin bit, so the
        // conversion is lossless in both directions.
        builder.Property(u => u.Role)
            .HasColumnName("Admin")
            .HasConversion(role => role == UserRole.Admin, isAdmin => isAdmin ? UserRole.Admin : UserRole.User);

        // Nullable unique index - SQLite (like most databases) treats each NULL as distinct, so any number of
        // local (EntraObjectId == null) accounts can coexist with any number of SSO accounts.
        builder.HasIndex(u => u.EntraObjectId).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Ignore(u => u.IsLocalAccount);

        builder.HasOne(u => u.JobRole).WithMany().HasForeignKey(u => u.JobRoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(u => u.Team).WithMany().HasForeignKey(u => u.TeamId).OnDelete(DeleteBehavior.Restrict);
    }
}
