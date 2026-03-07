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
        builder.Property(a => a.Id).ValueGeneratedNever();

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

        builder.Property(a => a.Category)
            .HasConversion<int>();

        builder.HasOne(a => a.Assignee)
            .WithMany(u => u.AssignedActions)
            .HasForeignKey(a => a.AssigneeId)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
