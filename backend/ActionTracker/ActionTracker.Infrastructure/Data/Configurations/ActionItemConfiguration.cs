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
