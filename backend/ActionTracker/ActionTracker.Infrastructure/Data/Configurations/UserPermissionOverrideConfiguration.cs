using ActionTracker.Application.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class UserPermissionOverrideConfiguration : IEntityTypeConfiguration<UserPermissionOverride>
{
    public void Configure(EntityTypeBuilder<UserPermissionOverride> builder)
    {
        builder.ToTable("UserPermissionOverrides");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(u => u.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(u => u.UserDisplayName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.Area)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(u => u.Action)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(u => u.OrgUnitScope)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(u => u.OrgUnitId)
            .IsRequired(false);

        builder.Property(u => u.OrgUnitName)
            .IsRequired(false)
            .HasMaxLength(256);

        builder.Property(u => u.Reason)
            .IsRequired(false)
            .HasMaxLength(1000);

        builder.Property(u => u.ExpiresAt)
            .IsRequired(false);

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true);

        builder.Property(u => u.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(u => u.CreatedBy)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(u => u.UpdatedBy)
            .IsRequired(false)
            .HasMaxLength(450);

        builder.Property(u => u.IsDeleted)
            .HasDefaultValue(false);

        // Composite index for the most common lookup: "what overrides exist for this user in this area?"
        builder.HasIndex(u => new { u.UserId, u.Area, u.Action });

        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}
