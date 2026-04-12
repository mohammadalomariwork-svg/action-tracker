using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.ToTable("EmailTemplates");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.TemplateKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(e => e.TemplateKey)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Subject)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.HtmlBody)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.IsActive)
            .HasDefaultValue(true);

        builder.Property(e => e.Description)
            .HasMaxLength(500);

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false);
    }
}
