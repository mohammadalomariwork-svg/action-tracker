using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class WorkCalendarConfiguration : IEntityTypeConfiguration<WorkCalendar>
{
    public void Configure(EntityTypeBuilder<WorkCalendar> builder)
    {
        builder.ToTable("WorkCalendars");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Description)
            .HasMaxLength(500);

        builder.Property(c => c.TimeZoneId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(c => c.CreatedBy)
            .HasMaxLength(450);

        builder.Property(c => c.UpdatedBy)
            .HasMaxLength(450);

        builder.HasMany(c => c.Days)
            .WithOne(d => d.WorkCalendar)
            .HasForeignKey(d => d.WorkCalendarId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Exceptions)
            .WithOne(e => e.WorkCalendar)
            .HasForeignKey(e => e.WorkCalendarId)
            .OnDelete(DeleteBehavior.Cascade);

        // Name is unique among non-deleted calendars.
        builder.HasIndex(c => c.Name)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        // At most one default calendar — the database refuses a second one.
        builder.HasIndex(c => c.IsDefault)
            .IsUnique()
            .HasFilter("[IsDefault] = 1 AND [IsDeleted] = 0");

        // Query filter for soft delete
        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
