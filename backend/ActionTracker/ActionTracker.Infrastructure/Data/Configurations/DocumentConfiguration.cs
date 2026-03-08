using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(d => d.FileName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.ContentType)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.FileSize)
            .IsRequired();

        builder.Property(d => d.Content)
            .IsRequired()
            .HasColumnType("varbinary(max)");

        builder.Property(d => d.RelatedEntityType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.RelatedEntityId)
            .IsRequired();

        builder.Property(d => d.UploadedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(d => d.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(d => d.UploadedBy)
            .WithMany()
            .HasForeignKey(d => d.UploadedByUserId)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite index for fast lookup by entity
        builder.HasIndex(d => new { d.RelatedEntityType, d.RelatedEntityId });
    }
}
