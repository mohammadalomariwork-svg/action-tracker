using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

/// <summary>
/// Internal project classifications of a workspace (WO-2026-11 P-1). The query
/// filter (soft delete + private workspace) lives in <c>AppDbContext</c>; the
/// project's link is configured in <see cref="ProjectConfiguration"/>.
/// </summary>
public class WorkspaceProjectClassificationConfiguration : IEntityTypeConfiguration<WorkspaceProjectClassification>
{
    public void Configure(EntityTypeBuilder<WorkspaceProjectClassification> builder)
    {
        builder.ToTable("WorkspaceProjectClassifications");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.NormalizedName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.IsActive)
            .HasDefaultValue(true);

        builder.Property(c => c.IsDeleted)
            .HasDefaultValue(false);

        builder.Property(c => c.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(c => c.CreatedByUserId)
            .HasMaxLength(450);

        builder.Property(c => c.UpdatedByUserId)
            .HasMaxLength(450);

        // Restrict: workspaces are only soft-deleted, and a classification never moves.
        builder.HasOne(c => c.Workspace)
            .WithMany()
            .HasForeignKey(c => c.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        // One live classification per name per workspace, whatever the case;
        // a deleted name may be used again.
        builder.HasIndex(c => new { c.WorkspaceId, c.NormalizedName })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(c => new { c.WorkspaceId, c.SortOrder });
    }
}
