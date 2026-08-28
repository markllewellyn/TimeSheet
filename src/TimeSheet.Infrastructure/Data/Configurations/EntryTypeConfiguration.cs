using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeSheet.Domain.Entities;

namespace TimeSheet.Infrastructure.Data.Configurations;

/// <summary>Genuinely new, no legacy equivalent - see EntryType.cs.</summary>
public class EntryTypeConfiguration : IEntityTypeConfiguration<EntryType>
{
    public void Configure(EntityTypeBuilder<EntryType> builder)
    {
        builder.ToTable("EntryTypes");
        builder.Property(t => t.Name).HasMaxLength(50).IsRequired();

        // A project can't have two entry types of the same name, but the same name is fine reused across
        // different projects (e.g. every project having its own "Travel" entry type).
        builder.HasIndex(t => new { t.ProjectId, t.Name }).IsUnique();

        builder.HasOne(t => t.Project).WithMany().HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
