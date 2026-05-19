using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class StrategicObjectiveConfiguration : IEntityTypeConfiguration<StrategicObjective>
{
    public void Configure(EntityTypeBuilder<StrategicObjective> builder)
    {
        builder.ToTable("StrategicObjectives");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(s => s.ObjectiveCode)
            .IsRequired()
            .HasMaxLength(20);

        // Filtered unique — drafts intentionally re-use their published
        // counterpart's ObjectiveCode (same logical objective), so the index
        // must only enforce uniqueness on the live (non-draft, non-deleted)
        // row set. CR-001-G.
        builder.HasIndex(s => s.ObjectiveCode)
            .IsUnique()
            .HasFilter("[IsDraft] = 0 AND [IsDeleted] = 0");

        builder.Property(s => s.Statement)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(s => s.Description)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(s => s.OrgUnitId)
            .IsRequired();

        builder.Property(s => s.IsDeleted)
            .HasDefaultValue(false);

        builder.Property(s => s.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(s => s.CreatedBy).HasMaxLength(256);
        builder.Property(s => s.UpdatedBy).HasMaxLength(256);
        builder.Property(s => s.DeletedBy).HasMaxLength(256);

        builder.HasOne(s => s.OrgUnit)
            .WithMany(o => o.StrategicObjectives)
            .HasForeignKey(s => s.OrgUnitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(s => s.StrategicPlanId)
            .HasColumnType("uniqueidentifier");

        builder.HasIndex(s => s.StrategicPlanId);

        builder.HasOne(s => s.StrategicPlan)
            .WithMany(p => p.Objectives)
            .HasForeignKey(s => s.StrategicPlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
