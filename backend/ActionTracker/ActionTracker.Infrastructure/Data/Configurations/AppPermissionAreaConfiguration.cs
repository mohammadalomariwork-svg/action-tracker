using ActionTracker.Application.Permissions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class AppPermissionAreaConfiguration : IEntityTypeConfiguration<AppPermissionArea>
{
    public void Configure(EntityTypeBuilder<AppPermissionArea> builder)
    {
        builder.ToTable("PermissionAreas");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.Description)
            .IsRequired(false)
            .HasMaxLength(500);

        builder.Property(a => a.DisplayOrder)
            .IsRequired();

        builder.Property(a => a.IsActive)
            .HasDefaultValue(true);

        builder.Property(a => a.IsDeleted)
            .HasDefaultValue(false);

        builder.Property(a => a.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(a => a.CreatedBy)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasIndex(a => a.Name).IsUnique();

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
