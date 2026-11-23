using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Calendars.DTOs;
using ActionTracker.Application.Features.Calendars.Interfaces;
using ActionTracker.Application.Features.Calendars.Validators;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Features.Calendars.Services;

public class WorkCalendarService : IWorkCalendarService
{
    private readonly IAppDbContext _db;
    private readonly IWorkspaceAccessResolver _workspaceAccess;
    private readonly WorkCalendarCache _cache;
    private readonly ILogger<WorkCalendarService> _logger;

    public WorkCalendarService(
        IAppDbContext db,
        IWorkspaceAccessResolver workspaceAccess,
        WorkCalendarCache cache,
        ILogger<WorkCalendarService> logger)
    {
        _db              = db;
        _workspaceAccess = workspaceAccess;
        _cache           = cache;
        _logger          = logger;
    }

    // ── Admin CRUD ────────────────────────────────────────────────────────────

    public async Task<List<WorkCalendarDto>> GetAllAsync(bool includeDeleted = false, CancellationToken ct = default)
    {
        IQueryable<WorkCalendar> query = _db.WorkCalendars
            .AsNoTracking()
            .Include(c => c.Days)
            .Include(c => c.Exceptions);

        // Calendars carry no workspace data, so lifting the soft-delete filter
        // here cannot expose a private workspace.
        if (includeDeleted)
            query = query.IgnoreQueryFilters();

        var calendars = await query
            .OrderByDescending(c => c.IsDefault)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);

        return calendars.Select(MapToDto).ToList();
    }

    public async Task<WorkCalendarDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var calendar = await _db.WorkCalendars
            .AsNoTracking()
            .Include(c => c.Days)
            .Include(c => c.Exceptions)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        return calendar is null ? null : MapToDto(calendar);
    }

    public async Task<WorkCalendarDto> CreateAsync(
        CreateWorkCalendarDto dto, string userId, CancellationToken ct = default)
    {
        EnsureValid(new CreateWorkCalendarDtoValidator(), dto);

        var name = dto.Name.Trim();
        await EnsureNameIsFreeAsync(name, exceptCalendarId: null, ct);

        // There is always a default (seeded at startup). Should it ever be
        // missing, the first active calendar created takes the role.
        var hasDefault = await _db.WorkCalendars.AnyAsync(c => c.IsDefault, ct);

        var calendar = new WorkCalendar
        {
            Id          = Guid.NewGuid(),
            Name        = name,
            Description = NullIfBlank(dto.Description),
            TimeZoneId  = dto.TimeZoneId.Trim(),
            IsActive    = dto.IsActive,
            IsDefault   = !hasDefault && dto.IsActive,
            CreatedAt   = DateTime.UtcNow,
            CreatedBy   = userId,
        };

        foreach (var day in dto.Days)
        {
            var row = new WorkCalendarDay { WorkCalendarId = calendar.Id, DayOfWeek = day.DayOfWeek };
            Apply(day, row);
            calendar.Days.Add(row);
        }

        foreach (var exception in dto.Exceptions ?? new List<WorkCalendarExceptionDto>())
        {
            var row = new WorkCalendarException { WorkCalendarId = calendar.Id };
            Apply(exception, row);
            calendar.Exceptions.Add(row);
        }

        _db.WorkCalendars.Add(calendar);
        await _db.SaveChangesAsync(ct);
        _cache.Clear();

        _logger.LogInformation("Work calendar {Name} ({Id}) created by {UserId}", calendar.Name, calendar.Id, userId);

        return MapToDto(calendar);
    }

    public async Task<WorkCalendarDto> UpdateAsync(
        Guid id, UpdateWorkCalendarDto dto, string userId, CancellationToken ct = default)
    {
        EnsureValid(new UpdateWorkCalendarDtoValidator(), dto);

        var calendar = await _db.WorkCalendars
            .Include(c => c.Days)
            .Include(c => c.Exceptions)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException($"Calendar {id} not found.");

        var name = dto.Name.Trim();
        await EnsureNameIsFreeAsync(name, exceptCalendarId: id, ct);

        if (calendar.IsActive && !dto.IsActive)
            await EnsureNotDefaultOrInUseAsync(calendar, "deactivated", ct);

        calendar.Name        = name;
        calendar.Description = NullIfBlank(dto.Description);
        calendar.TimeZoneId  = dto.TimeZoneId.Trim();
        calendar.IsActive    = dto.IsActive;
        Touch(calendar, userId);

        // Weekly pattern: update the seven rows in place.
        foreach (var day in dto.Days)
        {
            var row = calendar.Days.FirstOrDefault(d => d.DayOfWeek == day.DayOfWeek);
            if (row is null)
            {
                // Rows are added through the DbSet so EF inserts them: a new
                // entity with a preset key that is only reachable through a
                // tracked parent would be treated as an existing row.
                row = new WorkCalendarDay { WorkCalendarId = calendar.Id, DayOfWeek = day.DayOfWeek };
                _db.WorkCalendarDays.Add(row);
            }

            Apply(day, row);
        }

        // Exceptions: the request carries the full list.
        var incoming = dto.Exceptions ?? new List<WorkCalendarExceptionDto>();
        var existing = calendar.Exceptions.ToDictionary(e => e.Id);

        var unknownId = incoming.FirstOrDefault(e => e.Id.HasValue && !existing.ContainsKey(e.Id.Value));
        if (unknownId is not null)
            throw new ArgumentException($"Exception {unknownId.Id} does not belong to this calendar.");

        var keptIds = incoming.Where(e => e.Id.HasValue).Select(e => e.Id!.Value).ToHashSet();
        foreach (var removed in existing.Values.Where(e => !keptIds.Contains(e.Id)))
            _db.WorkCalendarExceptions.Remove(removed);

        foreach (var exception in incoming)
        {
            if (exception.Id.HasValue)
            {
                Apply(exception, existing[exception.Id.Value]);
            }
            else
            {
                var row = new WorkCalendarException { WorkCalendarId = calendar.Id };
                Apply(exception, row);
                _db.WorkCalendarExceptions.Add(row);
            }
        }

        await _db.SaveChangesAsync(ct);
        _cache.Clear();

        _logger.LogInformation("Work calendar {Id} updated by {UserId}", id, userId);

        return (await GetByIdAsync(id, ct))!;
    }

    public async Task SoftDeleteAsync(Guid id, string userId, CancellationToken ct = default)
    {
        var calendar = await _db.WorkCalendars.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException($"Calendar {id} not found.");

        await EnsureNotDefaultOrInUseAsync(calendar, "deleted", ct);

        calendar.IsDeleted = true;
        Touch(calendar, userId);

        await _db.SaveChangesAsync(ct);
        _cache.Clear();

        _logger.LogInformation("Work calendar {Id} soft-deleted by {UserId}", id, userId);
    }

    public async Task RestoreAsync(Guid id, string userId, CancellationToken ct = default)
    {
        // Calendars carry no workspace data, so lifting the soft-delete filter
        // here cannot expose a private workspace.
        var calendar = await _db.WorkCalendars
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException($"Calendar {id} not found.");

        if (!calendar.IsDeleted) return;

        var nameTaken = await _db.WorkCalendars
            .AnyAsync(c => c.Name.ToLower() == calendar.Name.ToLower(), ct);
        if (nameTaken)
            throw new InvalidOperationException(
                $"Cannot restore: another calendar is already named '{calendar.Name}'. Rename that calendar first.");

        calendar.IsDeleted = false;
        calendar.IsDefault = false; // a deleted calendar is never the default
        Touch(calendar, userId);

        await _db.SaveChangesAsync(ct);
        _cache.Clear();

        _logger.LogInformation("Work calendar {Id} restored by {UserId}", id, userId);
    }

    public async Task<WorkCalendarDto> SetDefaultAsync(Guid id, string userId, CancellationToken ct = default)
    {
        var calendar = await _db.WorkCalendars.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException($"Calendar {id} not found.");

        if (!calendar.IsActive)
            throw new InvalidOperationException("An inactive calendar cannot be the default. Activate it first.");

        if (!calendar.IsDefault)
        {
            // Step 1: release the flag. Step 2: claim it. Two saves, in this
            // order, because the database allows only one default row at any
            // moment (filtered unique index) and EF does not order a one-save
            // swap reliably. The transaction makes the pair atomic: nobody
            // ever sees "no default", and a failure rolls both steps back.
            await using var transaction = await BeginTransactionIfSupportedAsync(ct);
            try
            {
                var previousDefaults = await _db.WorkCalendars
                    .Where(c => c.IsDefault && c.Id != id)
                    .ToListAsync(ct);

                foreach (var previous in previousDefaults)
                {
                    previous.IsDefault = false;
                    Touch(previous, userId);
                }

                if (previousDefaults.Count > 0)
                    await _db.SaveChangesAsync(ct);

                calendar.IsDefault = true;
                Touch(calendar, userId);
                await _db.SaveChangesAsync(ct);

                if (transaction is not null)
                    await transaction.CommitAsync(ct);
            }
            finally
            {
                _cache.Clear();
            }

            _logger.LogInformation("Work calendar {Id} set as default by {UserId}", id, userId);
        }

        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<WorkCalendarUsageDto> GetUsageAsync(Guid id, string userId, CancellationToken ct = default)
    {
        var calendar = await _db.WorkCalendars
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException($"Calendar {id} not found.");

        var total = await CountWorkspacesUsingAsync(id, ct);

        // Named list: the global query filter already hides private workspaces
        // the caller is not part of; the access resolver narrows it further for
        // non-privileged roles (they only see workspaces they belong to).
        var visibleIds = await _workspaceAccess.GetVisibleWorkspaceIdsAsync(userId);

        IQueryable<Workspace> visible = _db.Workspaces.AsNoTracking();
        if (visibleIds is not null)
            visible = visible.Where(w => visibleIds.Contains(w.Id));

        var workspaces = await visible
            .Where(w => w.WorkCalendarId == id)
            .OrderBy(w => w.Title)
            .Select(w => new WorkCalendarUsageWorkspaceDto
            {
                Id       = w.Id,
                Title    = w.Title,
                IsActive = w.IsActive,
            })
            .ToListAsync(ct);

        var inheriting = calendar.IsDefault
            ? await visible.CountAsync(w => w.WorkCalendarId == null && w.IsActive, ct)
            : 0;

        return new WorkCalendarUsageDto
        {
            CalendarId               = id,
            IsDefault                = calendar.IsDefault,
            TotalWorkspaceCount      = total,
            Workspaces               = workspaces,
            HiddenWorkspaceCount     = Math.Max(0, total - workspaces.Count),
            InheritingWorkspaceCount = inheriting,
        };
    }

    // ── Calculation API ───────────────────────────────────────────────────────

    private const string NoDefaultMessage = "No default work calendar is configured.";

    public async Task<ResolvedWorkCalendar> GetDefaultAsync(CancellationToken ct = default) =>
        await FindDefaultAsync(ct) ?? throw new InvalidOperationException(NoDefaultMessage);

    public async Task<ResolvedWorkCalendar> GetForWorkspaceAsync(Guid workspaceId, CancellationToken ct = default) =>
        await FindForWorkspaceAsync(workspaceId, ct) ?? throw new InvalidOperationException(NoDefaultMessage);

    public async Task EnsureWorkingDaysAsync(
        Guid? workspaceId, IReadOnlyCollection<WorkingDateCheck> checks, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(checks);

        // Q8: only dates that are created or changed. No change = no lookup.
        var changed = checks.Where(c => c.IsChanged).ToList();
        if (changed.Count == 0) return;

        var calendar = workspaceId.HasValue
            ? await FindForWorkspaceAsync(workspaceId.Value, ct)
            : await FindDefaultAsync(ct);

        // The seeder guarantees a default calendar. Should it ever be missing,
        // saving work must not stop because of it: skip the rule and say so.
        if (calendar is null)
        {
            _logger.LogWarning(
                "No work calendar is configured; the working-day check was skipped for workspace {WorkspaceId}.",
                workspaceId);
            return;
        }

        var refused = new List<NonWorkingDate>();
        foreach (var check in changed)
        {
            var date = check.Date!.Value;
            if (calendar.IsWorkingDay(date)) continue;

            refused.Add(new NonWorkingDate(check.Field, date, TryNextWorkingDay(calendar, date)));
        }

        if (refused.Count > 0)
            throw new NonWorkingDateException(calendar.Name, refused);
    }

    // ── Workspace setting ─────────────────────────────────────────────────────

    /// <summary>Default window of <see cref="GetWorkspaceCalendarAsync"/>: one year back.</summary>
    public const int DefaultWindowDaysBack = 366;

    /// <summary>Default window of <see cref="GetWorkspaceCalendarAsync"/>: three years ahead.</summary>
    public const int DefaultWindowDaysAhead = 3 * 366;

    /// <summary>Longest window a caller may ask for (the working-day search limit).</summary>
    public const int MaxWindowDays = 3660;

    public async Task<WorkspaceCalendarDto> GetWorkspaceCalendarAsync(
        Guid workspaceId, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default)
    {
        var today       = DateOnly.FromDateTime(DateTime.UtcNow);
        var windowStart = from ?? (to?.AddDays(-(DefaultWindowDaysBack + DefaultWindowDaysAhead)) ?? today.AddDays(-DefaultWindowDaysBack));
        var windowEnd   = to   ?? windowStart.AddDays(DefaultWindowDaysBack + DefaultWindowDaysAhead);

        if (windowEnd < windowStart)
            throw new ArgumentException("'to' must be on or after 'from'.");
        if (windowEnd.DayNumber - windowStart.DayNumber + 1 > MaxWindowDays)
            throw new ArgumentException($"The window may cover at most {MaxWindowDays} days.");

        // Normal workspace query filter: a private workspace the caller is
        // not part of is reported as not found, never as "uses the default".
        var workspace = await _db.Workspaces
            .AsNoTracking()
            .Where(w => w.Id == workspaceId)
            .Select(w => new { w.Id, w.WorkCalendarId })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Workspace {workspaceId} not found.");

        return await BuildWorkspaceCalendarAsync(workspace.Id, workspace.WorkCalendarId, windowStart, windowEnd, ct);
    }

    public async Task<WorkspaceCalendarDto> SetWorkspaceCalendarAsync(
        Guid workspaceId, SetWorkspaceCalendarDto dto, string userId, CancellationToken ct = default)
    {
        EnsureValid(new SetWorkspaceCalendarDtoValidator(), dto);

        // Global filter hides private workspaces the caller is not part of;
        // the resolver narrows non-privileged roles to their own workspaces.
        var workspace = await _db.Workspaces.FirstOrDefaultAsync(w => w.Id == workspaceId, ct)
            ?? throw new KeyNotFoundException($"Workspace {workspaceId} not found.");

        var visibleIds = await _workspaceAccess.GetVisibleWorkspaceIdsAsync(userId);
        if (visibleIds is not null && !visibleIds.Contains(workspaceId))
            throw new KeyNotFoundException($"Workspace {workspaceId} not found.");

        if (dto.WorkCalendarId is { } calendarId)
        {
            // The soft-delete filter on calendars hides deleted ones.
            var calendar = await _db.WorkCalendars
                .AsNoTracking()
                .Where(c => c.Id == calendarId)
                .Select(c => new { c.IsActive, c.Name })
                .FirstOrDefaultAsync(ct)
                ?? throw new ArgumentException($"Calendar {calendarId} does not exist or has been deleted.");

            if (!calendar.IsActive)
                throw new ArgumentException($"Calendar '{calendar.Name}' is inactive. Activate it before assigning it to a workspace.");
        }

        if (workspace.WorkCalendarId != dto.WorkCalendarId)
        {
            var previous = workspace.WorkCalendarId;
            workspace.WorkCalendarId = dto.WorkCalendarId;
            workspace.UpdatedAt      = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            // No cache clear: the cache holds calendars by id, and the
            // workspace → calendar link is read from the database every time.
            _logger.LogInformation(
                "Workspace {WorkspaceId} calendar changed from {Previous} to {Current} by {UserId}",
                workspaceId, previous?.ToString() ?? "default", dto.WorkCalendarId?.ToString() ?? "default", userId);
        }

        return await GetWorkspaceCalendarAsync(workspaceId, ct: ct);
    }

    public async Task<List<WorkspaceCalendarOptionDto>> GetWorkspaceCalendarOptionsAsync(CancellationToken ct = default)
    {
        var calendars = await _db.WorkCalendars
            .AsNoTracking()
            .Include(c => c.Days)
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.IsDefault)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);

        return calendars
            .Select(c => new WorkspaceCalendarOptionDto
            {
                Id          = c.Id,
                Name        = c.Name,
                Description = c.Description,
                TimeZoneId  = c.TimeZoneId,
                IsDefault   = c.IsDefault,
                Days        = MapDays(c.Days),
            })
            .ToList();
    }

    // ── Calendar resolution ───────────────────────────────────────────────────

    /// <summary>The default calendar, or null when none is configured.</summary>
    private async Task<ResolvedWorkCalendar?> FindDefaultAsync(CancellationToken ct)
    {
        if (_cache.TryGetDefault(out var cached))
            return cached;

        var version = _cache.Version;

        var calendar = await _db.WorkCalendars
            .AsNoTracking()
            .Include(c => c.Days)
            .Include(c => c.Exceptions)
            .FirstOrDefaultAsync(c => c.IsDefault, ct);

        if (calendar is null) return null;

        var resolved = new ResolvedWorkCalendar(calendar);
        _cache.Set(resolved, version);
        return resolved;
    }

    /// <summary>The workspace's calendar, else the default, else null when none is configured.</summary>
    private async Task<ResolvedWorkCalendar?> FindForWorkspaceAsync(Guid workspaceId, CancellationToken ct)
    {
        // Goes through the normal workspace query filter: a workspace the
        // caller cannot see resolves to the default calendar, like one that
        // has no calendar of its own.
        var calendarId = await _db.Workspaces
            .Where(w => w.Id == workspaceId)
            .Select(w => w.WorkCalendarId)
            .FirstOrDefaultAsync(ct);

        if (calendarId is null)
            return await FindDefaultAsync(ct);

        if (_cache.TryGet(calendarId.Value, out var cached))
            return cached;

        var version = _cache.Version;

        var calendar = await _db.WorkCalendars
            .AsNoTracking()
            .Include(c => c.Days)
            .Include(c => c.Exceptions)
            .FirstOrDefaultAsync(c => c.Id == calendarId.Value && c.IsActive, ct);

        // The guards stop an in-use calendar being deleted or deactivated, so
        // this only happens after a direct database edit. Fall back safely.
        if (calendar is null)
        {
            _logger.LogWarning(
                "Workspace {WorkspaceId} points at calendar {CalendarId}, which is missing or inactive; using the default.",
                workspaceId, calendarId);
            return await FindDefaultAsync(ct);
        }

        var resolved = new ResolvedWorkCalendar(calendar);
        _cache.Set(resolved, version);
        return resolved;
    }

    /// <summary>
    /// Same fallback as <see cref="FindForWorkspaceAsync"/> (own calendar when
    /// active, else the default), read straight from the database because the
    /// page needs the rows, not just the answers.
    /// </summary>
    private async Task<WorkspaceCalendarDto> BuildWorkspaceCalendarAsync(
        Guid workspaceId, Guid? ownCalendarId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        WorkCalendar? calendar = null;
        if (ownCalendarId is { } id)
            calendar = await LoadCalendarAsync(c => c.Id == id && c.IsActive, from, to, ct);

        var inherited = calendar is null;
        calendar ??= await LoadCalendarAsync(c => c.IsDefault, from, to, ct)
            ?? throw new InvalidOperationException(NoDefaultMessage);

        return new WorkspaceCalendarDto
        {
            WorkspaceId    = workspaceId,
            WorkCalendarId = ownCalendarId,
            CalendarId     = calendar.Id,
            CalendarName   = calendar.Name,
            TimeZoneId     = calendar.TimeZoneId,
            IsDefault      = calendar.IsDefault,
            IsInherited    = inherited,
            From           = from,
            To             = to,
            Days           = MapDays(calendar.Days),
            Exceptions     = MapExceptions(calendar.Exceptions),
        };
    }

    private Task<WorkCalendar?> LoadCalendarAsync(
        System.Linq.Expressions.Expression<Func<WorkCalendar, bool>> predicate,
        DateOnly from, DateOnly to, CancellationToken ct) =>
        _db.WorkCalendars
            .AsNoTracking()
            .Include(c => c.Days)
            .Include(c => c.Exceptions.Where(e => e.StartDate <= to && e.EndDate >= from))
            .FirstOrDefaultAsync(predicate, ct);

    private static DateOnly? TryNextWorkingDay(ResolvedWorkCalendar calendar, DateOnly date)
    {
        try
        {
            return calendar.NextWorkingDay(date);
        }
        catch (InvalidOperationException)
        {
            // No working day within the search limit: refuse without a suggestion.
            return null;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens a database transaction on a real database. Returns null on the
    /// EF InMemory provider (unit and integration tests), which has none.
    /// </summary>
    private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync(CancellationToken ct)
    {
        if (_db is DbContext context
            && context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
        {
            return await context.Database.BeginTransactionAsync(ct);
        }

        return null;
    }

    private static void EnsureValid<T>(IValidator<T> validator, T dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var result = validator.Validate(dto);
        if (!result.IsValid)
            throw new ArgumentException(string.Join(" ", result.Errors.Select(e => e.ErrorMessage).Distinct()));
    }

    private async Task EnsureNameIsFreeAsync(string name, Guid? exceptCalendarId, CancellationToken ct)
    {
        var lowered = name.ToLower();
        var taken = await _db.WorkCalendars
            .AnyAsync(c => c.Name.ToLower() == lowered && c.Id != exceptCalendarId, ct);

        if (taken)
            throw new InvalidOperationException($"A calendar named '{name}' already exists.");
    }

    /// <summary>
    /// Guard for delete and deactivate: the default calendar and any calendar
    /// a workspace points at must stay available.
    /// </summary>
    private async Task EnsureNotDefaultOrInUseAsync(WorkCalendar calendar, string verb, CancellationToken ct)
    {
        if (calendar.IsDefault)
            throw new InvalidOperationException(
                $"The default calendar cannot be {verb}. Make another calendar the default first.");

        var inUse = await CountWorkspacesUsingAsync(calendar.Id, ct);
        if (inUse > 0)
            throw new InvalidOperationException(
                $"This calendar is used by {inUse} workspace{(inUse == 1 ? "" : "s")} and cannot be {verb}. " +
                "Move those workspaces to another calendar first.");
    }

    /// <summary>
    /// Counts every workspace pointing at the calendar — deliberately without
    /// query filters, so private and archived workspaces also protect their
    /// calendar. Only the number leaves this method, never workspace data.
    /// </summary>
    private Task<int> CountWorkspacesUsingAsync(Guid calendarId, CancellationToken ct) =>
        _db.Workspaces
            .IgnoreQueryFilters()
            .CountAsync(w => w.WorkCalendarId == calendarId, ct);

    private static void Touch(WorkCalendar calendar, string userId)
    {
        calendar.UpdatedAt = DateTime.UtcNow;
        calendar.UpdatedBy = userId;
    }

    private static void Apply(WorkCalendarDayDto dto, WorkCalendarDay row)
    {
        row.IsWorkingDay = dto.IsWorkingDay;
        // A non-working day carries no hours.
        row.StartTime = dto.IsWorkingDay ? dto.StartTime : null;
        row.EndTime   = dto.IsWorkingDay ? dto.EndTime   : null;
    }

    private static void Apply(WorkCalendarExceptionDto dto, WorkCalendarException row)
    {
        row.Name         = dto.Name.Trim();
        row.StartDate    = dto.StartDate;
        row.EndDate      = dto.EndDate;
        row.IsWorkingDay = dto.IsWorkingDay;
        // A non-working exception (holiday) carries no hours.
        row.StartTime = dto.IsWorkingDay ? dto.StartTime : null;
        row.EndTime   = dto.IsWorkingDay ? dto.EndTime   : null;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static WorkCalendarDto MapToDto(WorkCalendar c) => new()
    {
        Id          = c.Id,
        Name        = c.Name,
        Description = c.Description,
        TimeZoneId  = c.TimeZoneId,
        IsDefault   = c.IsDefault,
        IsActive    = c.IsActive,
        IsDeleted   = c.IsDeleted,
        CreatedAt   = c.CreatedAt,
        CreatedBy   = c.CreatedBy,
        UpdatedAt   = c.UpdatedAt,
        UpdatedBy   = c.UpdatedBy,
        Days        = MapDays(c.Days),
        Exceptions  = MapExceptions(c.Exceptions),
    };

    private static List<WorkCalendarDayDto> MapDays(IEnumerable<WorkCalendarDay> days) =>
        days
            .OrderBy(d => d.DayOfWeek)
            .Select(d => new WorkCalendarDayDto
            {
                DayOfWeek    = d.DayOfWeek,
                IsWorkingDay = d.IsWorkingDay,
                StartTime    = d.StartTime,
                EndTime      = d.EndTime,
            })
            .ToList();

    private static List<WorkCalendarExceptionDto> MapExceptions(IEnumerable<WorkCalendarException> exceptions) =>
        exceptions
            .OrderBy(e => e.StartDate)
            .ThenBy(e => e.Name)
            .Select(e => new WorkCalendarExceptionDto
            {
                Id           = e.Id,
                Name         = e.Name,
                StartDate    = e.StartDate,
                EndDate      = e.EndDate,
                IsWorkingDay = e.IsWorkingDay,
                StartTime    = e.StartTime,
                EndTime      = e.EndTime,
            })
            .ToList();
}
