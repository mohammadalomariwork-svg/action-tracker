using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class ActionItemWorkflowRequestConfiguration : IEntityTypeConfiguration<ActionItemWorkflowRequest>
{
    public void Configure(EntityTypeBuilder<ActionItemWorkflowRequest> builder)
    {
        builder.ToTable("ActionItemWorkflowRequests");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWSEQUENTIALID()")
            .ValueGeneratedOnAdd();

        builder.HasOne(w => w.ActionItem)
            .WithMany(a => a.WorkflowRequests)
            .HasForeignKey(w => w.ActionItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(w => w.RequestType)
            .HasConversion<int>();

        builder.Property(w => w.Status)
            .HasConversion<int>()
            .HasDefaultValue(WorkflowRequestStatus.Pending);

        builder.Property(w => w.RequestedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(w => w.RequestedByDisplayName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(w => w.RequestedNewStartDate)
            .HasColumnType("datetime2");

        builder.Property(w => w.RequestedNewDueDate)
            .HasColumnType("datetime2");

        builder.Property(w => w.RequestedNewStatus)
            .HasConversion<int?>();

        builder.Property(w => w.CurrentStartDate)
            .HasColumnType("datetime2");

        builder.Property(w => w.CurrentDueDate)
            .HasColumnType("datetime2");

        builder.Property(w => w.CurrentStatus)
            .HasConversion<int?>();

        builder.Property(w => w.Reason)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(w => w.ReviewedByUserId)
            .HasMaxLength(450);

        builder.Property(w => w.ReviewedByDisplayName)
            .HasMaxLength(256);

        builder.Property(w => w.ReviewComment)
            .HasMaxLength(2000);

        builder.Property(w => w.ReviewedAt)
            .HasColumnType("datetime2");

        builder.Property(w => w.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(w => w.IsDeleted)
            .HasDefaultValue(false);

        builder.HasIndex(w => w.ActionItemId)
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(w => w.RequestedByUserId);

        builder.HasIndex(w => w.Status);
    }
}
