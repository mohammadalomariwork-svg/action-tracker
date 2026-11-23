using System.Collections.Concurrent;

namespace ActionTracker.Application.Features.Calendars.Services;

/// <summary>
/// Process-wide cache of resolved calendars, registered as a singleton.
/// <see cref="WorkCalendarService"/> clears it after every calendar write.
/// Entries also expire after <see cref="TimeToLive"/> so that a second app
/// instance (or a direct database edit) cannot serve a stale calendar for long.
/// </summary>
public sealed class WorkCalendarCache
{
    public static readonly TimeSpan TimeToLive = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<Guid, Entry> _calendars = new();
    private readonly Func<DateTime> _utcNow;
    private readonly object _gate = new();

    // Guid.Empty = "default calendar not cached".
    private Guid _defaultCalendarId;
    private long _version;

    public WorkCalendarCache() : this(() => DateTime.UtcNow)
    {
    }

    /// <summary>Test seam: lets a test control the clock used for expiry.</summary>
    public WorkCalendarCache(Func<DateTime> utcNow)
    {
        _utcNow = utcNow;
    }

    /// <summary>
    /// Changes every time the cache is cleared. A reader takes it before
    /// loading from the database and hands it back to <see cref="Set"/>, so a
    /// calendar read before a concurrent write is never cached after it.
    /// </summary>
    public long Version => Interlocked.Read(ref _version);

    public bool TryGet(Guid calendarId, out ResolvedWorkCalendar calendar)
    {
        if (_calendars.TryGetValue(calendarId, out var entry) && !IsExpired(entry.CachedAtUtc))
        {
            calendar = entry.Calendar;
            return true;
        }

        calendar = null!;
        return false;
    }

    /// <summary>The cached default calendar, when one is cached and still fresh.</summary>
    public bool TryGetDefault(out ResolvedWorkCalendar calendar)
    {
        Guid defaultId;
        lock (_gate)
        {
            defaultId = _defaultCalendarId;
        }

        if (defaultId != Guid.Empty && TryGet(defaultId, out calendar))
            return true;

        calendar = null!;
        return false;
    }

    /// <summary>
    /// Caches <paramref name="calendar"/> unless the cache was cleared since
    /// <paramref name="versionWhenLoaded"/> was read.
    /// </summary>
    public void Set(ResolvedWorkCalendar calendar, long versionWhenLoaded)
    {
        lock (_gate)
        {
            if (versionWhenLoaded != _version) return;

            _calendars[calendar.Id] = new Entry(calendar, _utcNow());
            if (calendar.IsDefault)
                _defaultCalendarId = calendar.Id;
        }
    }

    /// <summary>Drops everything. Called after any calendar is saved.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _version++;
            _defaultCalendarId = Guid.Empty;
            _calendars.Clear();
        }
    }

    private bool IsExpired(DateTime cachedAtUtc) => _utcNow() - cachedAtUtc > TimeToLive;

    private readonly record struct Entry(ResolvedWorkCalendar Calendar, DateTime CachedAtUtc);
}
