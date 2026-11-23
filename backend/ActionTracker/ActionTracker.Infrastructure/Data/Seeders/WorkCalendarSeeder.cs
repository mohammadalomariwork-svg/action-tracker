using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Data.Seeders;

/// <summary>
/// Guarantees a default work calendar exists. Idempotent.
/// <list type="bullet">
/// <item>No calendar at all (clean database, or an existing database on its
/// first start after the <c>Add_WorkCalendars</c> migration): inserts
/// "KU Standard" — Monday–Friday 08:00–17:00, Saturday–Sunday off, Asia/Dubai —
/// as the default.</item>
/// <item>Calendars exist but none is the default (only possible after a direct
/// database edit): promotes the oldest active one.</item>
/// </list>
/// </summary>
public static class WorkCalendarSeeder
{
    public const string DefaultCalendarName = "KU Standard";

    private static readonly TimeOnly WorkStart = new(8, 0);
    private static readonly TimeOnly WorkEnd   = new(17, 0);

    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        var anyCalendar = await db.WorkCalendars.IgnoreQueryFilters().AnyAsync();
        if (!anyCalendar)
        {
            db.WorkCalendars.Add(BuildKuStandard());
            await db.SaveChangesAsync();
            logger.LogInformation("WorkCalendarSeeder: inserted default calendar '{Name}'.", DefaultCalendarName);
            return;
        }

        if (await db.WorkCalendars.AnyAsync(c => c.IsDefault))
        {
            logger.LogInformation("WorkCalendarSeeder: default calendar already present, nothing to insert.");
            return;
        }

        var promoted = await db.WorkCalendars
            .Where(c => c.IsActive)
            .OrderBy(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (promoted is null)
        {
            // Every remaining calendar is inactive or deleted — add the standard one.
            var nameTaken = await db.WorkCalendars.AnyAsync(c => c.Name == DefaultCalendarName);
            var calendar  = BuildKuStandard();
            if (nameTaken)
                calendar.Name = $"{DefaultCalendarName} ({DateTime.UtcNow:yyyy-MM-dd HHmmss})";

            db.WorkCalendars.Add(calendar);
            await db.SaveChangesAsync();
            logger.LogWarning("WorkCalendarSeeder: no usable calendar found; inserted default calendar '{Name}'.", calendar.Name);
            return;
        }

        promoted.IsDefault = true;
        promoted.UpdatedAt = DateTime.UtcNow;
        promoted.UpdatedBy = "system";
        await db.SaveChangesAsync();
        logger.LogWarning("WorkCalendarSeeder: no default calendar found; promoted '{Name}' to default.", promoted.Name);
    }

    private static WorkCalendar BuildKuStandard()
    {
        var calendar = new WorkCalendar
        {
            Id          = Guid.NewGuid(),
            Name        = DefaultCalendarName,
            Description = "Khalifa University standard working week: Monday to Friday, 08:00–17:00.",
            TimeZoneId  = WorkCalendar.DefaultTimeZoneId,
            IsDefault   = true,
            IsActive    = true,
            CreatedAt   = DateTime.UtcNow,
            CreatedBy   = "system",
        };

        foreach (var dayOfWeek in Enum.GetValues<DayOfWeek>())
        {
            var isWorkingDay = dayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
            calendar.Days.Add(new WorkCalendarDay
            {
                WorkCalendarId = calendar.Id,
                DayOfWeek      = dayOfWeek,
                IsWorkingDay   = isWorkingDay,
                StartTime      = isWorkingDay ? WorkStart : null,
                EndTime        = isWorkingDay ? WorkEnd   : null,
            });
        }

        return calendar;
    }
}
