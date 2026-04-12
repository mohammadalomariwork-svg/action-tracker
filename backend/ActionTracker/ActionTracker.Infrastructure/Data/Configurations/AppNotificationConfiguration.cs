using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class AppNotificationConfiguration : IEntityTypeConfiguration<AppNotification>
{
    public void Configure(EntityTypeBuilder<AppNotification> builder)
    {
        builder.ToTable("AppNotifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(n => n.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(n => n.Type)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(n => n.ActionType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(n => n.RelatedEntityType)
            .HasMaxLength(100);

        builder.Property(n => n.RelatedEntityCode)
            .HasMaxLength(50);

        builder.Property(n => n.Url)
            .HasMaxLength(500);

        builder.Property(n => n.IsRead)
            .HasDefaultValue(false);

        builder.Property(n => n.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(n => n.CreatedByUserId)
            .HasMaxLength(450);

        builder.Property(n => n.CreatedByDisplayName)
            .HasMaxLength(200);

        // Main query pattern: unread notifications for a user, newest first
        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt })
            .IsDescending(false, false, true);

        // All notifications for a user, newest first
        builder.HasIndex(n => new { n.UserId, n.CreatedAt })
            .IsDescending(false, true);

        // Lookup by related entity
        builder.HasIndex(n => new { n.RelatedEntityType, n.RelatedEntityId });
    }
}
