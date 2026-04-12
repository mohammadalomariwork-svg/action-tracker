using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class ProjectRiskConfiguration : IEntityTypeConfiguration<ProjectRisk>
{
    public void Configure(EntityTypeBuilder<ProjectRisk> builder)
    {
        builder.ToTable("ProjectRisks");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(r => r.RiskCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(r => r.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(r => r.Category)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.MitigationPlan)
            .HasMaxLength(2000);

        builder.Property(r => r.ContingencyPlan)
            .HasMaxLength(2000);

        builder.Property(r => r.RiskOwnerUserId)
            .HasMaxLength(450);

        builder.Property(r => r.RiskOwnerDisplayName)
            .HasMaxLength(200);

        builder.Property(r => r.Notes)
            .HasMaxLength(2000);

        builder.Property(r => r.CreatedByUserId)
            .HasMaxLength(450);

        builder.Property(r => r.CreatedByDisplayName)
            .HasMaxLength(200);

        builder.Property(r => r.RiskRating)
            .HasConversion<int>();

        builder.Property(r => r.Status)
            .HasConversion<int>()
            .HasDefaultValue(RiskStatus.Open);

        builder.Property(r => r.IdentifiedDate)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(r => r.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(r => r.IsDeleted)
            .HasDefaultValue(false);

        // FK to Project
        builder.HasOne(r => r.Project)
            .WithMany(p => p.Risks)
            .HasForeignKey(r => r.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(r => new { r.ProjectId, r.IsDeleted });

        builder.HasIndex(r => new { r.RiskCode, r.ProjectId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        // Query filter for soft delete
        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
