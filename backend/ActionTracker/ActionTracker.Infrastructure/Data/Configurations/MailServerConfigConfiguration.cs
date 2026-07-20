using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class MailServerConfigConfiguration : IEntityTypeConfiguration<MailServerConfig>
{
    public void Configure(EntityTypeBuilder<MailServerConfig> builder)
    {
        builder.ToTable("MailServerConfigs");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(m => m.Host).IsRequired().HasMaxLength(256);
        builder.Property(m => m.Port).IsRequired();
        builder.Property(m => m.UseSsl).IsRequired();

        builder.Property(m => m.FromEmail).IsRequired().HasMaxLength(256);
        builder.Property(m => m.FromName).IsRequired().HasMaxLength(256);

        builder.Property(m => m.Username).HasMaxLength(256);
        builder.Property(m => m.EncryptedPassword).HasMaxLength(4000);

        builder.Property(m => m.IsActive).HasDefaultValue(true);

        builder.Property(m => m.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(m => m.CreatedBy).HasMaxLength(256);
        builder.Property(m => m.UpdatedBy).HasMaxLength(256);

        // Only one active config at a time
        builder.HasIndex(m => m.IsActive)
            .IsUnique()
            .HasFilter("[IsActive] = 1")
            .HasDatabaseName("IX_MailServerConfigs_IsActive_Unique");
    }
}
