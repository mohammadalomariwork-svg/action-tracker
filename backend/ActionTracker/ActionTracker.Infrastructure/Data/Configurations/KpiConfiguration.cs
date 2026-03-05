using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class KpiConfiguration : IEntityTypeConfiguration<Kpi>
{
    public void Configure(EntityTypeBuilder<Kpi> builder)
    {
        builder.ToTable("Kpis");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.Id)
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(k => k.KpiNumber)
            .IsRequired();

        builder.Property(k => k.Name)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(k => k.Description)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(k => k.CalculationMethod)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(k => k.Period)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(k => k.Unit)
            .HasMaxLength(50);

        builder.Property(k => k.StrategicObjectiveId)
            .IsRequired();

        builder.HasIndex(k => new { k.StrategicObjectiveId, k.KpiNumber })
            .IsUnique();

        builder.Property(k => k.IsDeleted)
            .HasDefaultValue(false);

        builder.Property(k => k.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(k => k.StrategicObjective)
            .WithMany(s => s.Kpis)
            .HasForeignKey(k => k.StrategicObjectiveId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
