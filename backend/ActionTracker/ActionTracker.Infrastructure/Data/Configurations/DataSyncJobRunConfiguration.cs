using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class DataSyncJobRunConfiguration : IEntityTypeConfiguration<DataSyncJobRun>
{
    public void Configure(EntityTypeBuilder<DataSyncJobRun> builder)
    {
        builder.ToTable("DataSyncJobRuns");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(r => r.StartedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(r => r.TriggeredBy).HasMaxLength(256);
        builder.Property(r => r.Message).HasMaxLength(4000);

        builder.HasIndex(r => new { r.JobId, r.StartedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_DataSyncJobRuns_JobId_StartedAt");

        builder.HasIndex(r => r.Status)
            .HasDatabaseName("IX_DataSyncJobRuns_Status");
    }
}
