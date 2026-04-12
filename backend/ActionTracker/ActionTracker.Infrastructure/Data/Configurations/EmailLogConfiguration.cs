using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    public void Configure(EntityTypeBuilder<EmailLog> builder)
    {
        builder.ToTable("EmailLogs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.TemplateKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(e => e.TemplateKey);

        builder.Property(e => e.ToEmail)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Subject)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.SentAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(e => e.SentAt)
            .IsDescending(true);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(e => e.RelatedEntityType)
            .HasMaxLength(100);

        builder.HasIndex(e => new { e.RelatedEntityType, e.RelatedEntityId });

        builder.Property(e => e.SentByUserId)
            .HasMaxLength(450);
    }
}
