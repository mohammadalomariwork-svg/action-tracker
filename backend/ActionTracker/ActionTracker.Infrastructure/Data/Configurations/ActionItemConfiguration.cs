using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class ActionItemConfiguration : IEntityTypeConfiguration<ActionItem>
{
    public void Configure(EntityTypeBuilder<ActionItem> builder)
    {
        builder.ToTable("ActionItems");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.ActionId)
            .IsRequired()
            .HasMaxLength(10);

        builder.HasIndex(a => a.ActionId)
            .IsUnique();

        builder.Property(a => a.Title)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.Description)
            .HasMaxLength(5000);

        builder.Property(a => a.Progress)
            .HasDefaultValue(0);

        builder.ToTable(t => t.HasCheckConstraint("CK_ActionItems_Progress", "[Progress] >= 0 AND [Progress] <= 100"));

        builder.Property(a => a.Status)
            .HasConversion<int>();

        builder.Property(a => a.Priority)
            .HasConversion<int>();

        // FK to Workspace
        builder.HasOne(a => a.Workspace)
            .WithMany()
            .HasForeignKey(a => a.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.WorkspaceId);

        // FK to Project (optional) – NoAction to avoid multiple cascade paths via Project→Milestone→ActionItem
        builder.HasOne(a => a.Project)
            .WithMany()
            .HasForeignKey(a => a.ProjectId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(a => a.ProjectId);

        // FK to Milestone (optional)
        builder.HasOne(a => a.Milestone)
            .WithMany()
            .HasForeignKey(a => a.MilestoneId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => a.MilestoneId);

        builder.Property(a => a.IsStandalone)
            .HasDefaultValue(true);

        builder.Property(a => a.CreatedByUserId)
            .HasMaxLength(450);
    }
}

public class ActionItemEscalationConfiguration : IEntityTypeConfiguration<ActionItemEscalation>
{
    public void Configure(EntityTypeBuilder<ActionItemEscalation> builder)
    {
        builder.ToTable("ActionItemEscalations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Explanation)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(e => e.EscalatedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(e => e.ActionItem)
            .WithMany(a => a.Escalations)
            .HasForeignKey(e => e.ActionItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.EscalatedByUser)
            .WithMany()
            .HasForeignKey(e => e.EscalatedByUserId)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.ActionItemId);
    }
}

public class ActionItemAssigneeConfiguration : IEntityTypeConfiguration<ActionItemAssignee>
{
    public void Configure(EntityTypeBuilder<ActionItemAssignee> builder)
    {
        builder.ToTable("ActionItemAssignees");

        builder.HasKey(aa => new { aa.ActionItemId, aa.UserId });

        builder.HasOne(aa => aa.ActionItem)
            .WithMany(a => a.Assignees)
            .HasForeignKey(aa => aa.ActionItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(aa => aa.User)
            .WithMany(u => u.ActionItemAssignments)
            .HasForeignKey(aa => aa.UserId)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(aa => aa.UserId)
            .IsRequired()
            .HasMaxLength(450);
    }
}

public class ActionItemCommentConfiguration : IEntityTypeConfiguration<ActionItemComment>
{
    public void Configure(EntityTypeBuilder<ActionItemComment> builder)
    {
        builder.ToTable("ActionItemComments");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(c => c.Content)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(c => c.AuthorUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(c => c.IsHighImportance)
            .HasDefaultValue(false);

        builder.Property(c => c.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(c => c.ActionItem)
            .WithMany(a => a.Comments)
            .HasForeignKey(c => c.ActionItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorUserId)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.ActionItemId);
    }
}
