using ActionTracker.Application.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(r => r.RoleName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(r => r.Area)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.Action)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.OrgUnitScope)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.OrgUnitId)
            .IsRequired(false);

        builder.Property(r => r.OrgUnitName)
            .IsRequired(false)
            .HasMaxLength(256);

        builder.Property(r => r.IsActive)
            .HasDefaultValue(true);

        builder.Property(r => r.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(r => r.CreatedBy)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(r => r.UpdatedBy)
            .IsRequired(false)
            .HasMaxLength(450);

        builder.Property(r => r.IsDeleted)
            .HasDefaultValue(false);

        // Composite index for the most common lookup: "what actions is this role allowed in this area?"
        builder.HasIndex(r => new { r.RoleName, r.Area, r.Action });

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
