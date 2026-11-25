using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

/// <summary>Workspace tags (WO-2026-11 A-5). Query filters live in <c>AppDbContext</c>.</summary>
public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(t => t.NormalizedName)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(t => t.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(t => t.CreatedByUserId)
            .HasMaxLength(450);

        // Restrict: workspaces are only soft-deleted, and a tag never moves.
        builder.HasOne(t => t.Workspace)
            .WithMany()
            .HasForeignKey(t => t.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.CreatedByUserId)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Restrict);

        // One tag per name per workspace, whatever the case.
        builder.HasIndex(t => new { t.WorkspaceId, t.NormalizedName })
            .IsUnique();
    }
}

public class ActionItemTagConfiguration : IEntityTypeConfiguration<ActionItemTag>
{
    public void Configure(EntityTypeBuilder<ActionItemTag> builder)
    {
        builder.ToTable("ActionItemTags");

        builder.HasKey(x => new { x.ActionItemId, x.TagId });

        // Action items are only ever soft-deleted; cascade mirrors the other
        // action-item children (assignees, sub-items, updates).
        builder.HasOne(x => x.ActionItem)
            .WithMany(a => a.Tags)
            .HasForeignKey(x => x.ActionItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: tags are never deleted, and no second cascade path from
        // Workspaces to ActionItemTags.
        builder.HasOne(x => x.Tag)
            .WithMany(t => t.ActionItems)
            .HasForeignKey(x => x.TagId)
            .OnDelete(DeleteBehavior.Restrict);

        // Tag filter on the lists, item counts per tag
        builder.HasIndex(x => x.TagId);
    }
}
