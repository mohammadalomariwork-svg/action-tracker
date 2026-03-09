using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class MilestoneConfiguration : IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> builder)
    {
        builder.ToTable("Milestones");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(m => m.MilestoneCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(m => m.MilestoneCode)
            .IsUnique();

        builder.Property(m => m.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(m => m.Description)
            .HasMaxLength(5000);

        builder.Property(m => m.Status)
            .HasConversion<int>()
            .HasDefaultValue(MilestoneStatus.NotStarted);

        builder.Property(m => m.CompletionPercentage)
            .HasColumnType("decimal(5,2)")
            .HasDefaultValue(0m);

        builder.Property(m => m.Weight)
            .HasColumnType("decimal(5,2)")
            .HasDefaultValue(0m);

        builder.Property(m => m.IsDeadlineFixed)
            .HasDefaultValue(false);

        builder.Property(m => m.IsDeleted)
            .HasDefaultValue(false);

        builder.Property(m => m.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(m => m.ApproverUserId)
            .HasMaxLength(450);

        // FK to Project
        builder.HasOne(m => m.Project)
            .WithMany()
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to Approver (ApplicationUser, optional)
        builder.HasOne(m => m.Approver)
            .WithMany()
            .HasForeignKey(m => m.ApproverUserId)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.ProjectId);
        builder.HasIndex(m => m.Status);
        builder.HasIndex(m => new { m.ProjectId, m.SequenceOrder });

        // Query filter for soft delete
        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}
