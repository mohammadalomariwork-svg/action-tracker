using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class KpiActualConfiguration : IEntityTypeConfiguration<KpiActual>
{
    public void Configure(EntityTypeBuilder<KpiActual> builder)
    {
        builder.ToTable("KpiActuals");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWSEQUENTIALID()")
            .ValueGeneratedOnAdd();

        builder.HasOne(a => a.KpiTarget)
            .WithMany()
            .HasForeignKey(a => a.KpiTargetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(a => a.PeriodType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(a => a.PeriodIndex).IsRequired();
        builder.Property(a => a.Year).IsRequired();

        builder.Property(a => a.ActualValue)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(a => a.Notes).HasMaxLength(2000);

        builder.Property(a => a.Status)
            .HasConversion<int>()
            .HasDefaultValue(KpiActualStatus.Draft);

        builder.Property(a => a.SubmittedByUserId).HasMaxLength(450);
        builder.Property(a => a.SubmittedByDisplayName).HasMaxLength(256);
        builder.Property(a => a.AssignedReviewerUserId).HasMaxLength(450);
        builder.Property(a => a.AssignedReviewerDisplayName).HasMaxLength(256);
        builder.Property(a => a.ReviewedByUserId).HasMaxLength(450);
        builder.Property(a => a.ReviewedByDisplayName).HasMaxLength(256);
        builder.Property(a => a.ReviewerComment).HasMaxLength(2000);
        builder.Property(a => a.CreatedByUserId).HasMaxLength(450);
        builder.Property(a => a.ModifiedByUserId).HasMaxLength(450);

        builder.Property(a => a.CreatedAtUtc).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(a => a.IsDeleted).HasDefaultValue(false);

        // Indexes
        builder.HasIndex(a => new { a.KpiTargetId, a.Year });
        builder.HasIndex(a => new { a.AssignedReviewerUserId, a.Status });

        // Filtered unique: at most one non-deleted, non-rejected actual per
        // (target, period, year). Rejected actuals are kept in history.
        builder.HasIndex(a => new { a.KpiTargetId, a.PeriodType, a.PeriodIndex, a.Year })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [Status] <> 3")
            .HasDatabaseName("IX_KpiActuals_TargetPeriod_NonRejected");

        builder.ToTable(tb =>
        {
            tb.HasCheckConstraint(
                "CK_KpiActuals_PeriodIndex",
                "([PeriodType] = 0 AND [PeriodIndex] BETWEEN 1 AND 4) " +
                "OR ([PeriodType] = 1 AND [PeriodIndex] BETWEEN 1 AND 2) " +
                "OR ([PeriodType] = 2 AND [PeriodIndex] = 1)");
        });
    }
}
