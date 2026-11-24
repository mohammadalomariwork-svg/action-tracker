using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

/// <summary>Sub-items on action items (WO-2026-11 A-2). Query filters live in <c>AppDbContext</c>.</summary>
public class ActionItemSubItemConfiguration : IEntityTypeConfiguration<ActionItemSubItem>
{
    public void Configure(EntityTypeBuilder<ActionItemSubItem> builder)
    {
        builder.ToTable("ActionItemSubItems");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(s => s.Description)
            .HasMaxLength(5000);

        builder.Property(s => s.SortOrder)
            .IsRequired();

        builder.Property(s => s.IsDone)
            .HasDefaultValue(false);

        builder.Property(s => s.IsDeleted)
            .HasDefaultValue(false);

        builder.Property(s => s.DeletedWithParent)
            .HasDefaultValue(false);

        builder.Property(s => s.DoneByUserId)
            .HasMaxLength(450);

        builder.Property(s => s.CreatedByUserId)
            .HasMaxLength(450);

        builder.Property(s => s.UpdatedByUserId)
            .HasMaxLength(450);

        builder.Property(s => s.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        // Action items are only ever soft-deleted; cascade mirrors the other
        // action-item children (assignees, comments, escalations).
        builder.HasOne(s => s.ActionItem)
            .WithMany(a => a.SubItems)
            .HasForeignKey(s => s.ActionItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: users are deactivated, never removed; and no second cascade
        // path from AspNetUsers to ActionItemSubItemAssignees.
        builder.HasOne(s => s.DoneByUser)
            .WithMany()
            .HasForeignKey(s => s.DoneByUserId)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.ActionItemId, s.SortOrder });
    }
}

public class ActionItemSubItemAssigneeConfiguration : IEntityTypeConfiguration<ActionItemSubItemAssignee>
{
    public void Configure(EntityTypeBuilder<ActionItemSubItemAssignee> builder)
    {
        builder.ToTable("ActionItemSubItemAssignees");

        builder.HasKey(a => new { a.SubItemId, a.UserId });

        builder.HasOne(a => a.SubItem)
            .WithMany(s => s.Assignees)
            .HasForeignKey(a => a.SubItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.UserId)
            .IsRequired()
            .HasMaxLength(450);

        // My Actions: "parents where I am assigned to a sub-item"
        builder.HasIndex(a => a.UserId);
    }
}
