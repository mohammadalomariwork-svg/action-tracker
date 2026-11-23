using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class WorkCalendarDayConfiguration : IEntityTypeConfiguration<WorkCalendarDay>
{
    public void Configure(EntityTypeBuilder<WorkCalendarDay> builder)
    {
        builder.ToTable("WorkCalendarDays");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(d => d.DayOfWeek)
            .HasConversion<int>();

        // One row per day of week per calendar.
        builder.HasIndex(d => new { d.WorkCalendarId, d.DayOfWeek })
            .IsUnique();

        // Matches the parent's soft-delete filter.
        builder.HasQueryFilter(d => !d.WorkCalendar.IsDeleted);
    }
}
