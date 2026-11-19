namespace ActionTracker.Infrastructure.DataSync;

/// <summary>
/// Best-effort English description for the handful of cron shapes an admin is
/// likely to pick. Anything else falls back to the raw expression.
/// </summary>
public static class CronDescriber
{
    private static readonly string[] Days =
        { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

    public static string Describe(string cron)
    {
        if (string.IsNullOrWhiteSpace(cron)) return string.Empty;

        var f = cron.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (f.Length != 5) return cron;

        var (min, hour, dom, mon, dow) = (f[0], f[1], f[2], f[3], f[4]);
        var allDays = dom == "*" && mon == "*" && dow == "*";

        // */N * * * *  → every N minutes
        if (min.StartsWith("*/") && hour == "*" && allDays && int.TryParse(min[2..], out var everyMin))
            return everyMin == 1 ? "Every minute" : $"Every {everyMin} minutes";

        // M * * * *  → hourly at minute M
        if (int.TryParse(min, out var m) && hour == "*" && allDays)
            return m == 0 ? "Every hour" : $"Every hour at :{m:00}";

        // M */N * * *  → every N hours
        if (int.TryParse(min, out m) && hour.StartsWith("*/") && allDays && int.TryParse(hour[2..], out var everyHour))
            return $"Every {everyHour} hours" + (m == 0 ? "" : $" at :{m:00}");

        // M H * * *  → daily
        if (int.TryParse(min, out m) && int.TryParse(hour, out var h) && allDays)
            return $"Daily at {h:00}:{m:00}";

        // M H * * D  → weekly
        if (int.TryParse(min, out m) && int.TryParse(hour, out h) && dom == "*" && mon == "*" && int.TryParse(dow, out var d) && d is >= 0 and <= 7)
            return $"Every {Days[d % 7]} at {h:00}:{m:00}";

        // M H D * *  → monthly
        if (int.TryParse(min, out m) && int.TryParse(hour, out h) && int.TryParse(dom, out var day) && mon == "*" && dow == "*")
            return $"Monthly on day {day} at {h:00}:{m:00}";

        return cron;
    }
}
