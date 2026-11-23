using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class WorkCalendarExceptionConfiguration : IEntityTypeConfiguration<WorkCalendarException>
{
    public void Configure(EntityTypeBuilder<WorkCalendarException> builder)
    {
        builder.ToTable("WorkCalendarExceptions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.HasIndex(e => new { e.WorkCalendarId, e.StartDate, e.EndDate });

        // Matches the parent's soft-delete filter.
        builder.HasQueryFilter(e => !e.WorkCalendar.IsDeleted);
    }
}
