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

        builder.HasIndex(s => s.ObjectiveCode)
            .IsUnique();

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

        builder.HasOne(s => s.OrgUnit)
            .WithMany(o => o.StrategicObjectives)
            .HasForeignKey(s => s.OrgUnitId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
