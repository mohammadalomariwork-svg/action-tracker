using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class StrategicPlanApprovalRequestConfiguration : IEntityTypeConfiguration<StrategicPlanApprovalRequest>
{
    public void Configure(EntityTypeBuilder<StrategicPlanApprovalRequest> builder)
    {
        builder.ToTable("StrategicPlanApprovalRequests");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWSEQUENTIALID()")
            .ValueGeneratedOnAdd();

        builder.HasOne(r => r.StrategicPlan)
            .WithMany()
            .HasForeignKey(r => r.StrategicPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(r => r.RequestType)
            .HasConversion<int>()
            .HasDefaultValue(StrategicPlanApprovalRequestType.Initial);

        builder.Property(r => r.Status)
            .HasConversion<int>()
            .HasDefaultValue(StrategicPlanApprovalStatus.Pending);

        builder.Property(r => r.RequestedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(r => r.RequestedByDisplayName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(r => r.AssignedReviewerUserId)
            .HasMaxLength(450);

        builder.Property(r => r.AssignedReviewerDisplayName)
            .HasMaxLength(256);

        builder.Property(r => r.ReviewedByUserId)
            .HasMaxLength(450);

        builder.Property(r => r.ReviewedByDisplayName)
            .HasMaxLength(256);

        builder.Property(r => r.Justification)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(r => r.ReviewerComment)
            .HasMaxLength(2000);

        builder.Property(r => r.ReviewedAt)
            .HasColumnType("datetime2");

        builder.Property(r => r.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(r => r.IsDeleted)
            .HasDefaultValue(false);

        // Multi-level sequential approval: Level is 1-based, TotalLevels is the
        // chain length snapshot at submit time. Defaults of 1/1 mean rows
        // back-filled from before the migration look like a single-level chain
        // and the existing UI continues to work unchanged.
        builder.Property(r => r.Level)
            .HasDefaultValue(1);

        builder.Property(r => r.TotalLevels)
            .HasDefaultValue(1);

        builder.HasIndex(r => new { r.StrategicPlanId, r.Status })
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(r => new { r.AssignedReviewerUserId, r.Status });

        builder.HasIndex(r => r.RequestedByUserId);
    }
}
