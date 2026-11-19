using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class DataSyncJobConfiguration : IEntityTypeConfiguration<DataSyncJob>
{
    public void Configure(EntityTypeBuilder<DataSyncJob> builder)
    {
        builder.ToTable("DataSyncJobs");

        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id)
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(j => j.JobKey).IsRequired().HasMaxLength(100);
        builder.HasIndex(j => j.JobKey).IsUnique().HasDatabaseName("IX_DataSyncJobs_JobKey");

        builder.Property(j => j.Name).IsRequired().HasMaxLength(200);
        builder.Property(j => j.Description).HasMaxLength(1000);
        builder.Property(j => j.CronExpression).IsRequired().HasMaxLength(100);
        builder.Property(j => j.TimeZoneId).HasMaxLength(100);
        builder.Property(j => j.IsEnabled).HasDefaultValue(true);

        builder.Property(j => j.LastRunMessage).HasMaxLength(2000);

        builder.Property(j => j.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(j => j.UpdatedBy).HasMaxLength(256);

        builder.HasMany(j => j.Runs)
            .WithOne(r => r.Job)
            .HasForeignKey(r => r.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
