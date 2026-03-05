using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class OrgUnitConfiguration : IEntityTypeConfiguration<OrgUnit>
{
    public void Configure(EntityTypeBuilder<OrgUnit> builder)
    {
        builder.ToTable("OrgUnits");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(o => o.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(o => o.Code)
            .HasMaxLength(50);

        builder.Property(o => o.Description)
            .HasMaxLength(500);

        builder.Property(o => o.Level)
            .IsRequired();

        builder.ToTable(t => t.HasCheckConstraint("CK_OrgUnits_Level", "[Level] BETWEEN 1 AND 10"));

        builder.Property(o => o.ParentId)
            .IsRequired(false);

        builder.Property(o => o.IsDeleted)
            .HasDefaultValue(false);

        builder.Property(o => o.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(o => o.Parent)
            .WithMany(o => o.Children)
            .HasForeignKey(o => o.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
