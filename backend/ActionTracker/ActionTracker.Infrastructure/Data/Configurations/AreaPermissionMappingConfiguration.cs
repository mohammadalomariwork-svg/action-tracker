using ActionTracker.Application.Permissions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class AreaPermissionMappingConfiguration : IEntityTypeConfiguration<AreaPermissionMapping>
{
    public void Configure(EntityTypeBuilder<AreaPermissionMapping> builder)
    {
        builder.ToTable("AreaPermissionMappings");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(m => m.AreaId)
            .IsRequired()
            .HasColumnType("uniqueidentifier");

        builder.Property(m => m.AreaName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.ActionId)
            .IsRequired()
            .HasColumnType("uniqueidentifier");

        builder.Property(m => m.ActionName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.IsActive)
            .HasDefaultValue(true);

        builder.Property(m => m.IsDeleted)
            .HasDefaultValue(false);

        builder.Property(m => m.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(m => m.CreatedBy)
            .IsRequired()
            .HasMaxLength(450);

        // Composite index: fast lookup of "which actions apply to this area?"
        builder.HasIndex(m => new { m.AreaId, m.ActionId });

        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}
