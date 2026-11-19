using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.Notifications.DTOs;
using ActionTracker.Application.Features.StrategicObjectives.DTOs;
using ActionTracker.Application.Features.StrategicObjectives.Interfaces;
using ActionTracker.Application.Features.StrategicPlans.Workflow;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActionTracker.Infrastructure.Services;

public class StrategicObjectiveService : IStrategicObjectiveService
{
    private readonly AppDbContext                        _context;
    private readonly IUserLookupService                 _userLookup;
    private readonly ILogger<StrategicObjectiveService> _logger;
    private readonly IEmailSender                       _emailSender;
    private readonly INotificationService               _notificationService;
    private readonly AppSettings                        _appSettings;
    private readonly IServiceScopeFactory               _scopeFactory;
    private readonly IStrategicScopeService             _scopeService;
    private readonly IStrategicPlanLockGuard            _lockGuard;
    private readonly IOrgUnitScopeResolver              _orgScopeResolver;

    public StrategicObjectiveService(
        AppDbContext                        context,
        IUserLookupService                 userLookup,
        ILogger<StrategicObjectiveService> logger,
        IEmailSender                       emailSender,
        INotificationService               notificationService,
        IOptions<AppSettings>              appSettings,
        IServiceScopeFactory               scopeFactory,
        IStrategicScopeService             scopeService,
        IStrategicPlanLockGuard            lockGuard,
        IOrgUnitScopeResolver              orgScopeResolver)
    {
        _context             = context;
        _userLookup          = userLookup;
        _logger              = logger;
        _emailSender         = emailSender;
        _notificationService = notificationService;
        _appSettings         = appSettings.Value;
        _scopeFactory        = scopeFactory;
        _scopeService        = scopeService;
        _lockGuard           = lockGuard;
        _orgScopeResolver    = orgScopeResolver;
    }

    // -------------------------------------------------------------------------
    // GetAllAsync
    // -------------------------------------------------------------------------

    public async Task<StrategicObjectiveListResponseDto> GetAllAsync(
        int               page,
        int               pageSize,
        Guid?             orgUnitId      = null,
        bool              includeDeleted = false,
        string?           currentUserId  = null,
        CancellationToken ct             = default)
    {
        try
        {
            var query = includeDeleted
                ? _context.StrategicObjectives.IgnoreQueryFilters()
                : _context.StrategicObjectives.AsQueryable();

            query = query
                .Include(o => o.OrgUnit)
                .Include(o => o.Kpis);

            if (orgUnitId.HasValue)
                query = query.Where(o => o.OrgUnitId == orgUnitId.Value);

            // Apply role-based scope (StrategyEditor, etc.) when caller is identified.
            if (currentUserId is not null)
            {
                var visible = await _scopeService.GetVisibleOrgUnitIdsAsync(currentUserId, ct);
                if (visible is not null)
                {
                    if (visible.Count == 0)
                        return new StrategicObjectiveListResponseDto
                        {
                            Objectives = new List<StrategicObjectiveDto>(),
                            TotalCount = 0,
                            Page       = page,
                            PageSize   = pageSize,
                        };
                    query = query.Where(o => visible.Contains(o.OrgUnitId));
                }
            }

            query = query.OrderBy(o => o.ObjectiveCode);

            var totalCount = await query.CountAsync(ct);

            var objectives = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var names = await ResolveNamesAsync(objectives, ct);

            var dtos = objectives
                .Select(o => MapToDto(o, kpiCount: o.Kpis.Count(k => !k.IsDeleted), names))
                .ToList();

            return new StrategicObjectiveListResponseDto
            {
                Objectives = dtos,
                TotalCount = totalCount,
                Page       = page,
                PageSize   = pageSize,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving strategic objectives (page={Page}, pageSize={PageSize}, orgUnitId={OrgUnitId})",
                page, pageSize, orgUnitId);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // GetByIdAsync
    // -------------------------------------------------------------------------

    public async Task<StrategicObjectiveDto?> GetByIdAsync(Guid id, string? currentUserId = null, CancellationToken ct = default)
    {
        try
        {
            var objective = await _context.StrategicObjectives
                .IgnoreQueryFilters()
                .Include(o => o.OrgUnit)
                .Include(o => o.Kpis)
                .Include(o => o.StrategicPlan)
                .FirstOrDefaultAsync(o => o.Id == id, ct);

            if (objective is null) return null;

            // Hide records outside the caller's scope by returning null
            // (look-by-id should not leak existence outside scope).
            if (currentUserId is not null)
            {
                var visible = await _scopeService.GetVisibleOrgUnitIdsAsync(currentUserId, ct);
                if (visible is not null && !visible.Contains(objective.OrgUnitId))
                    return null;
            }

            // Compute parent-plan lock state when the objective is wired to a plan
            // (StrategicPlanId is nullable during the migration window).
            bool? isPlanLocked = null;
            if (objective.StrategicPlanId is Guid planId)
                isPlanLocked = await _lockGuard.IsLockedAsync(planId, ct);

            var names = await ResolveNamesAsync([objective], ct);
            return MapToDto(objective, kpiCount: objective.Kpis.Count(k => !k.IsDeleted), names, isPlanLocked);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving strategic objective {Id}", id);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------

    public async Task<StrategicObjectiveDto> CreateAsync(
        CreateStrategicObjectiveRequestDto request,
        string                             userId,
        CancellationToken                  ct = default)
    {
        try
        {
            await _scopeService.EnsureCanWriteAsync(userId, request.OrgUnitId, ct);

            Guid? targetPlanId;

            // Track whether the destination plan is a draft so we can flag
            // the new objective row as a draft too (CR-001-G).
            bool targetPlanIsDraft = false;

            if (request.StrategicPlanId.HasValue)
            {
                // Caller is explicitly attaching to a known plan (Strategy page
                // flow). Validate that the plan exists, isn't archived, isn't
                // locked, and that the requested OrgUnitId falls inside the
                // plan's subtree (the plan's own unit or a descendant).
                // IgnoreQueryFilters so draft plans are reachable.
                var plan = await _context.StrategicPlans
                    .IgnoreQueryFilters()
                    .Where(p => p.Id == request.StrategicPlanId.Value && !p.IsDeleted)
                    .Select(p => new { p.Id, p.OrgUnitId, p.Status, p.IsDraft })
                    .FirstOrDefaultAsync(ct)
                    ?? throw new ArgumentException(
                        $"Strategic plan '{request.StrategicPlanId}' does not exist.",
                        nameof(request.StrategicPlanId));

                if (plan.Status == StrategicPlanStatus.Archived)
                    throw new InvalidOperationException(
                        "Cannot add objectives to an archived strategic plan.");

                await _lockGuard.EnsureNotLockedAsync(plan.Id, ct);

                var descendants = await _orgScopeResolver.GetDescendantOrgUnitIdsAsync(plan.OrgUnitId);
                var allowed = new HashSet<Guid>(descendants) { plan.OrgUnitId };
                if (!allowed.Contains(request.OrgUnitId))
                    throw new InvalidOperationException(
                        "The selected organisation unit is not within the strategic plan's subtree.");

                targetPlanId = plan.Id;
                targetPlanIsDraft = plan.IsDraft;
            }
            else
            {
                // Fallback (admin / legacy flow): auto-resolve the active plan
                // for the requested OrgUnitId via the org-unit-keyed lock guard,
                // then pick the newest non-archived plan for that unit. Drafts
                // are excluded from this fallback — callers wanting to add
                // objectives to a draft must pass StrategicPlanId explicitly.
                await _lockGuard.EnsureNotLockedForOrgUnitAsync(request.OrgUnitId, ct);

                targetPlanId = await _context.StrategicPlans
                    .Where(p => p.OrgUnitId == request.OrgUnitId
                             && p.Status != StrategicPlanStatus.Archived)
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => (Guid?)p.Id)
                    .FirstOrDefaultAsync(ct);
            }

            var orgUnit = await _context.OrgUnits
                .FirstOrDefaultAsync(o => o.Id == request.OrgUnitId, ct)
                ?? throw new ArgumentException(
                    $"Org unit '{request.OrgUnitId}' does not exist or has been deleted.",
                    nameof(request.OrgUnitId));

            // Atomic, collision-proof numbering: the DB sequence hands out
            // each number exactly once, is immune to concurrent creates and
            // to hard-deleted rows (unlike the previous COUNT(*)+1), and is
            // seeded from the highest existing code by the
            // Add_ObjectiveCodeSequence migration.
            // Fetched via raw ADO: EF's SqlQuery composes the statement into
            // a derived table (SELECT TOP(n) ... FROM (...)), where SQL
            // Server forbids NEXT VALUE FOR (error 11719).
            int nextNumber;
            var connection = _context.Database.GetDbConnection();
            var wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen)
                await _context.Database.OpenConnectionAsync(ct);
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT NEXT VALUE FOR dbo.ObjectiveCodeSeq;";
                command.Transaction =
                    _context.Database.CurrentTransaction?.GetDbTransaction();
                nextNumber = Convert.ToInt32(
                    await command.ExecuteScalarAsync(ct));
            }
            finally
            {
                if (!wasOpen)
                    await _context.Database.CloseConnectionAsync();
            }

            var objectiveCode = $"SO-{nextNumber}";

            var objective = new StrategicObjective
            {
                Id              = Guid.NewGuid(),
                ObjectiveCode   = objectiveCode,
                Statement       = request.Statement,
                Description     = request.Description,
                OrgUnitId       = request.OrgUnitId,
                StrategicPlanId = targetPlanId,
                IsDeleted       = false,
                // New objectives added inside a draft inherit the draft flag.
                // OriginalObjectiveId stays null — they are brand-new and will
                // be promoted as new published objectives at merge time.
                IsDraft         = targetPlanIsDraft,
                CreatedAt       = DateTime.UtcNow,
                CreatedBy       = userId,
            };

            _context.StrategicObjectives.Add(objective);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Created StrategicObjective {Id} '{Code}' for OrgUnit {OrgUnitId}",
                objective.Id, objectiveCode, request.OrgUnitId);

            // Fire-and-forget email notification
            var capturedObjective = objective;
            var capturedOrgUnit = orgUnit;
            var capturedUserId = userId;
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

                try
                {
                    var creatorName = await _userLookup.GetDisplayNameAsync(capturedUserId, CancellationToken.None);
                    var placeholders = new Dictionary<string, string>
                    {
                        ["ObjectiveCode"] = capturedObjective.ObjectiveCode,
                        ["Statement"]     = capturedObjective.Statement,
                        ["OrgUnit"]       = capturedOrgUnit.Name,
                        ["CreatedBy"]     = creatorName ?? capturedUserId,
                        ["ItemUrl"]       = $"{_appSettings.FrontendBaseUrl}/admin/objectives",
                    };

                    var creatorEmail = await db.Users
                        .Where(u => u.Id == capturedUserId && u.IsActive)
                        .Select(u => u.Email)
                        .FirstOrDefaultAsync();

                    if (creatorEmail is not null)
                    {
                        await emailSender.SendEmailAsync("StrategicObjective.Created", placeholders,
                            [creatorEmail], "StrategicObjective", capturedObjective.Id, capturedUserId);
                    }
                }
                catch (Exception ex2)
                {
                    _logger.LogError(ex2, "Error sending email for StrategicObjective.Created {Id}", capturedObjective.Id);
                }

                // In-app notification (self-notification for confirmation)
                try
                {
                    var creatorName2 = await _userLookup.GetDisplayNameAsync(capturedUserId, CancellationToken.None);
                    await notifService.CreateAsync(new CreateNotificationDto
                    {
                        UserId               = capturedUserId,
                        Title                = "Strategic Objective Created",
                        Message              = $"{capturedObjective.ObjectiveCode}: {capturedObjective.Statement}",
                        Type                 = "StrategicObjective",
                        ActionType           = "Created",
                        RelatedEntityType    = "StrategicObjective",
                        RelatedEntityId      = capturedObjective.Id,
                        RelatedEntityCode    = capturedObjective.ObjectiveCode,
                        Url                  = $"{_appSettings.FrontendBaseUrl}/admin/objectives",
                        CreatedByUserId      = capturedUserId,
                        CreatedByDisplayName = creatorName2,
                    });
                }
                catch (Exception ex3)
                {
                    _logger.LogError(ex3, "Error creating notification for StrategicObjective.Created {Id}", capturedObjective.Id);
                }
            });

            objective.OrgUnit = orgUnit;

            var names = await ResolveNamesAsync([objective], ct);
            return MapToDto(objective, kpiCount: 0, names);
        }
        catch (ArgumentException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating strategic objective");
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // UpdateAsync
    // -------------------------------------------------------------------------

    public async Task<StrategicObjectiveDto> UpdateAsync(
        Guid                               id,
        UpdateStrategicObjectiveRequestDto request,
        string                             userId,
        CancellationToken                  ct = default)
    {
        try
        {
            var objective = await _context.StrategicObjectives
                .IgnoreQueryFilters()
                .Include(o => o.Kpis)
                .FirstOrDefaultAsync(o => o.Id == id, ct)
                ?? throw new KeyNotFoundException($"Strategic objective '{id}' not found.");

            // Authorise both the existing org unit (so the user owns the record)
            // and the requested target org unit (so they cannot reassign out of scope).
            await _scopeService.EnsureCanWriteAsync(userId, objective.OrgUnitId, ct);
            await _scopeService.EnsureCanWriteAsync(userId, request.OrgUnitId, ct);
            // Draft objectives bypass the published lock — drafts are the
            // working copy for revisions (CR-001-G).
            if (!objective.IsDraft)
            {
                await _lockGuard.EnsureNotLockedForOrgUnitAsync(objective.OrgUnitId, ct);
                if (request.OrgUnitId != objective.OrgUnitId)
                    await _lockGuard.EnsureNotLockedForOrgUnitAsync(request.OrgUnitId, ct);
            }

            var orgUnit = await _context.OrgUnits
                .FirstOrDefaultAsync(o => o.Id == request.OrgUnitId, ct)
                ?? throw new ArgumentException(
                    $"Org unit '{request.OrgUnitId}' does not exist or has been deleted.",
                    nameof(request.OrgUnitId));

            objective.Statement   = request.Statement;
            objective.Description = request.Description;
            objective.OrgUnitId   = request.OrgUnitId;
            objective.UpdatedAt   = DateTime.UtcNow;
            objective.UpdatedBy   = userId;

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Updated StrategicObjective {Id}", id);

            objective.OrgUnit = orgUnit;

            var names = await ResolveNamesAsync([objective], ct);
            return MapToDto(objective, kpiCount: objective.Kpis.Count(k => !k.IsDeleted), names);
        }
        catch (KeyNotFoundException) { throw; }
        catch (ArgumentException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating strategic objective {Id}", id);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // SoftDeleteAsync
    // -------------------------------------------------------------------------

    public async Task SoftDeleteAsync(Guid id, string userId, CancellationToken ct = default)
    {
        try
        {
            // IgnoreQueryFilters so we can also delete draft objectives — they
            // are hidden from the default StrategicObjectives query.
            var objective = await _context.StrategicObjectives
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct)
                ?? throw new KeyNotFoundException($"Strategic objective '{id}' not found.");

            await _scopeService.EnsureCanWriteAsync(userId, objective.OrgUnitId, ct);

            // Lock guard only applies to objectives on the *published* plan.
            // Draft objectives are inherently editable while their parent
            // draft plan is in Draft status.
            if (!objective.IsDraft)
                await _lockGuard.EnsureNotLockedForOrgUnitAsync(objective.OrgUnitId, ct);

            // Block delete when projects still reference this objective. Caller
            // gets a 409 + the offending project codes so they can go unlink
            // them manually. For draft objectives we check the *published*
            // counterpart's references (Project.StrategicObjectiveId points
            // at published rows) — deleting the draft objective signals
            // intent to remove its published counterpart at promote time.
            var checkAgainstObjectiveId = objective.IsDraft && objective.OriginalObjectiveId.HasValue
                ? objective.OriginalObjectiveId.Value
                : objective.Id;
            var linkedProjects = await _context.Projects
                .Where(p => p.StrategicObjectiveId == checkAgainstObjectiveId && !p.IsDeleted)
                .Select(p => new { p.ProjectCode, p.Name })
                .ToListAsync(ct);

            if (linkedProjects.Count > 0)
            {
                var labels = linkedProjects
                    .Select(p => string.IsNullOrEmpty(p.ProjectCode) ? p.Name : $"{p.ProjectCode} – {p.Name}")
                    .ToList();
                throw new InvalidOperationException(
                    $"Cannot delete this strategic objective — {linkedProjects.Count} project(s) still link to it: " +
                    $"{string.Join("; ", labels)}. Unlink or reassign those projects first.");
            }

            var now = DateTime.UtcNow;
            objective.IsDeleted = true;
            objective.DeletedAt = now;
            objective.UpdatedAt = now;
            objective.DeletedBy = userId;

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Soft-deleted StrategicObjective {Id} (isDraft={IsDraft})",
                id, objective.IsDraft);
        }
        catch (KeyNotFoundException) { throw; }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error soft-deleting strategic objective {Id}", id);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // RestoreAsync
    // -------------------------------------------------------------------------

    public async Task RestoreAsync(Guid id, string userId, CancellationToken ct = default)
    {
        try
        {
            var objective = await _context.StrategicObjectives
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == id, ct)
                ?? throw new KeyNotFoundException($"Strategic objective '{id}' not found.");

            await _scopeService.EnsureCanWriteAsync(userId, objective.OrgUnitId, ct);

            objective.IsDeleted = false;
            objective.DeletedAt = null;
            objective.DeletedBy = null;
            objective.UpdatedAt = DateTime.UtcNow;
            objective.UpdatedBy = userId;

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Restored StrategicObjective {Id}", id);
        }
        catch (KeyNotFoundException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring strategic objective {Id}", id);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // GetByOrgUnitAsync
    // -------------------------------------------------------------------------

    public async Task<List<StrategicObjectiveDto>> GetByOrgUnitAsync(
        Guid              orgUnitId,
        string?           currentUserId = null,
        CancellationToken ct            = default)
    {
        try
        {
            // Apply role-based scope: hide the entire result if the requested
            // org unit is outside the caller's permitted set.
            if (currentUserId is not null)
            {
                var visible = await _scopeService.GetVisibleOrgUnitIdsAsync(currentUserId, ct);
                if (visible is not null && !visible.Contains(orgUnitId))
                    return new List<StrategicObjectiveDto>();
            }

            var objectives = await _context.StrategicObjectives
                .Include(o => o.OrgUnit)
                .Include(o => o.Kpis)
                .Where(o => o.OrgUnitId == orgUnitId)
                .OrderBy(o => o.ObjectiveCode)
                .ToListAsync(ct);

            var names = await ResolveNamesAsync(objectives, ct);

            return objectives
                .Select(o => MapToDto(o, kpiCount: o.Kpis.Count(k => !k.IsDeleted), names))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving strategic objectives for OrgUnit {OrgUnitId}", orgUnitId);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // GetByPlanAsync
    // -------------------------------------------------------------------------

    public async Task<List<StrategicObjectiveDto>> GetByPlanAsync(
        Guid              strategicPlanId,
        string?           currentUserId = null,
        CancellationToken ct            = default)
    {
        try
        {
            // Resolve the plan first so we can scope-check against its OrgUnit.
            // Returning empty (rather than throwing) when the plan is missing or
            // out-of-scope mirrors GetByOrgUnitAsync's behaviour.
            // IgnoreQueryFilters so callers can pass a draft plan id and see
            // its objectives (the global filter would otherwise return empty).
            var plan = await _context.StrategicPlans
                .IgnoreQueryFilters()
                .Where(p => p.Id == strategicPlanId && !p.IsDeleted)
                .Select(p => new { p.Id, p.OrgUnitId })
                .FirstOrDefaultAsync(ct);
            if (plan is null)
                return new List<StrategicObjectiveDto>();

            if (currentUserId is not null)
            {
                var visible = await _scopeService.GetVisibleOrgUnitIdsAsync(currentUserId, ct);
                if (visible is not null && !visible.Contains(plan.OrgUnitId))
                    return new List<StrategicObjectiveDto>();
            }

            var objectives = await _context.StrategicObjectives
                .IgnoreQueryFilters()
                .Include(o => o.OrgUnit)
                .Include(o => o.Kpis)
                .Where(o => o.StrategicPlanId == strategicPlanId && !o.IsDeleted)
                .OrderBy(o => o.ObjectiveCode)
                .ToListAsync(ct);

            var names = await ResolveNamesAsync(objectives, ct);

            return objectives
                .Select(o => MapToDto(o, kpiCount: o.Kpis.Count(k => !k.IsDeleted), names))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving strategic objectives for Plan {PlanId}", strategicPlanId);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<Dictionary<string, string>> ResolveNamesAsync(
        IEnumerable<StrategicObjective> objectives,
        CancellationToken               ct)
    {
        var ids = objectives
            .SelectMany(o => new[] { o.CreatedBy, o.UpdatedBy, o.DeletedBy })
            .Where(id => id != null)
            .Cast<string>()
            .Distinct();
        return await _userLookup.GetDisplayNamesAsync(ids, ct);
    }

    private static string? Resolve(string? userId, Dictionary<string, string> names)
        => userId != null && names.TryGetValue(userId, out var n) ? n : null;

    private static StrategicObjectiveDto MapToDto(
        StrategicObjective         o,
        int                        kpiCount,
        Dictionary<string, string> names,
        bool?                      isPlanLocked = null) =>
        new()
        {
            Id                  = o.Id,
            ObjectiveCode       = o.ObjectiveCode,
            Statement           = o.Statement,
            Description         = o.Description,
            OrgUnitId           = o.OrgUnitId,
            OrgUnitName         = o.OrgUnit?.Name ?? string.Empty,
            OrgUnitCode         = o.OrgUnit?.Code,
            IsDeleted           = o.IsDeleted,
            CreatedAt           = o.CreatedAt,
            UpdatedAt           = o.UpdatedAt,
            DeletedAt           = o.DeletedAt,
            CreatedBy           = o.CreatedBy,
            UpdatedBy           = o.UpdatedBy,
            DeletedBy           = o.DeletedBy,
            CreatedByName       = Resolve(o.CreatedBy, names),
            UpdatedByName       = Resolve(o.UpdatedBy, names),
            DeletedByName       = Resolve(o.DeletedBy, names),
            KpiCount            = kpiCount,
            // Parent plan info (only populated when the StrategicPlan navigation is included
            // and a non-null plan id is present).
            StrategicPlanId     = o.StrategicPlanId,
            StrategicPlanName   = o.StrategicPlan?.Name,
            StrategicPlanStatus = o.StrategicPlan?.Status,
            PlanStartYear       = o.StrategicPlan?.StartYear,
            PlanEndYear         = o.StrategicPlan?.EndYear,
            IsPlanLocked        = isPlanLocked,
        };
}
