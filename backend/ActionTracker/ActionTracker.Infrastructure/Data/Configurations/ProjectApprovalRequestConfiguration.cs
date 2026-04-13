using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class ProjectApprovalRequestConfiguration : IEntityTypeConfiguration<ProjectApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ProjectApprovalRequest> builder)
    {
        builder.ToTable("ProjectApprovalRequests");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWSEQUENTIALID()")
            .ValueGeneratedOnAdd();

        builder.HasOne(r => r.Project)
            .WithMany(p => p.ApprovalRequests)
            .HasForeignKey(r => r.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(r => r.Status)
            .HasConversion<int>()
            .HasDefaultValue(ProjectApprovalStatus.Pending);

        builder.Property(r => r.RequestedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(r => r.RequestedByDisplayName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(r => r.ReviewedByUserId)
            .HasMaxLength(450);

        builder.Property(r => r.ReviewedByDisplayName)
            .HasMaxLength(256);

        builder.Property(r => r.Reason)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(r => r.ReviewComment)
            .HasMaxLength(2000);

        builder.Property(r => r.ReviewedAt)
            .HasColumnType("datetime2");

        builder.Property(r => r.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(r => r.IsDeleted)
            .HasDefaultValue(false);

        builder.HasIndex(r => r.ProjectId)
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(r => r.RequestedByUserId);

        builder.HasIndex(r => r.Status);
    }
}
