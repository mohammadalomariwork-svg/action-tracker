using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class StrategicPlanApproverConfiguration : IEntityTypeConfiguration<StrategicPlanApprover>
{
    public void Configure(EntityTypeBuilder<StrategicPlanApprover> builder)
    {
        builder.ToTable("StrategicPlanApprovers");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWSEQUENTIALID()")
            .ValueGeneratedOnAdd();

        builder.HasOne(a => a.StrategicPlan)
            .WithMany(p => p.Approvers)
            .HasForeignKey(a => a.StrategicPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(a => a.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(a => a.Order)
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(a => a.CreatedBy)
            .HasMaxLength(450);

        // Order is unique per plan — guarantees a dense, deterministic chain.
        builder.HasIndex(a => new { a.StrategicPlanId, a.Order })
            .IsUnique();

        // The same user can't appear twice in one chain.
        builder.HasIndex(a => new { a.StrategicPlanId, a.UserId })
            .IsUnique();
    }
}
