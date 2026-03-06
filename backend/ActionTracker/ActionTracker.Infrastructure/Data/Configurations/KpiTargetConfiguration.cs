using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class KpiTargetConfiguration : IEntityTypeConfiguration<KpiTarget>
{
    public void Configure(EntityTypeBuilder<KpiTarget> builder)
    {
        builder.ToTable("KpiTargets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(t => t.KpiId)
            .IsRequired();

        builder.Property(t => t.Year)
            .IsRequired();

        builder.Property(t => t.Month)
            .IsRequired();

        builder.ToTable(tb => tb.HasCheckConstraint("CK_KpiTargets_Month", "[Month] BETWEEN 1 AND 12"));

        builder.Property(t => t.Target)
            .HasColumnType("decimal(18,4)");

        builder.Property(t => t.Actual)
            .HasColumnType("decimal(18,4)");

        builder.Property(t => t.Notes)
            .HasMaxLength(500);

        builder.Property(t => t.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(t => t.CreatedBy).HasMaxLength(256);
        builder.Property(t => t.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(t => new { t.KpiId, t.Year, t.Month })
            .IsUnique();

        builder.HasOne(t => t.Kpi)
            .WithMany(k => k.Targets)
            .HasForeignKey(t => t.KpiId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
