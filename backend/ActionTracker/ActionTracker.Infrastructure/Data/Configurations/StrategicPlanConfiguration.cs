using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class StrategicPlanConfiguration : IEntityTypeConfiguration<StrategicPlan>
{
    public void Configure(EntityTypeBuilder<StrategicPlan> builder)
    {
        builder.ToTable("StrategicPlans");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.OrgUnitId)
            .IsRequired();

        builder.Property(s => s.StartYear).IsRequired();
        builder.Property(s => s.DurationYears).IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<int>()
            .HasDefaultValue(Domain.Enums.StrategicPlanStatus.Draft);

        builder.Property(s => s.ApprovedByUserId).HasMaxLength(450);

        builder.Property(s => s.IsDeleted).HasDefaultValue(false);

        builder.Property(s => s.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(s => s.CreatedBy).HasMaxLength(256);
        builder.Property(s => s.UpdatedBy).HasMaxLength(256);
        builder.Property(s => s.DeletedBy).HasMaxLength(256);

        // Ignore the computed property for storage.
        builder.Ignore(s => s.EndYear);

        // Non-unique index for fast lookup by org unit.
        builder.HasIndex(s => s.OrgUnitId);

        // Filtered unique index: only one active (PendingApproval=1 OR Approved=2)
        // *published* plan per org unit. Drafts are excluded — a revision draft
        // sits alongside the Approved parent in the same OrgUnit while in-flight,
        // and its Status transitions to PendingApproval on submit (CR-001-G).
        builder.HasIndex(s => s.OrgUnitId)
            .IsUnique()
            .HasFilter("[IsDraft] = 0 AND [IsDeleted] = 0 AND [Status] IN (1, 2)")
            .HasDatabaseName("IX_StrategicPlans_OrgUnitId_ActiveUnique");

        builder.HasOne(s => s.OrgUnit)
            .WithMany(o => o.StrategicPlans)
            .HasForeignKey(s => s.OrgUnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
