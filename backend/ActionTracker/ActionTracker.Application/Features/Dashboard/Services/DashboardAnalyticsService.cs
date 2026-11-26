using ActionTracker.Application.Common.Extensions;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Dashboard.DTOs;
using ActionTracker.Application.Features.Dashboard.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Features.Dashboard.Services;

/// <summary>
/// Read-only aggregations for the dashboards.
///
/// Every query follows the same shape: filter to what the caller may see,
/// project only the handful of columns needed, and aggregate in memory with
/// the rules in <see cref="ActionItemRules"/>, <see cref="ProjectRules"/> and
/// <see cref="KpiAchievement"/>. Keeping the SQL to plain filters and column
/// projections means there is no grouping for the provider to translate, and
/// the counting rules live in one place that the drill-down filters share.
/// </summary>
public class DashboardAnalyticsService : IDashboardAnalyticsService
{
    private const int TrendWeeks          = 8;
    private const int MilestoneSoonDays   = 30;
    private const int MaxWorkspaceRows    = 12;
    private const int MaxMilestoneRows    = 12;

    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkspaceAccessResolver _workspaceAccess;
    private readonly IStrategicScopeService _strategicScope;
    private readonly ILogger<DashboardAnalyticsService> _logger;

    public DashboardAnalyticsService(
        IAppDbContext db,
        ICurrentUserService currentUser,
        IWorkspaceAccessResolver workspaceAccess,
        IStrategicScopeService strategicScope,
        ILogger<DashboardAnalyticsService> logger)
    {
        _db              = db;
        _currentUser     = currentUser;
        _workspaceAccess = workspaceAccess;
        _strategicScope  = strategicScope;
        _logger          = logger;
    }

    /// <summary>Overridable clock so tests can pin "today".</summary>
    protected virtual DateTime UtcNow => DateTime.UtcNow;

    // Slim projections ────────────────────────────────────────────────────────

    private sealed class ActionRow
    {
        public Guid           Id           { get; init; }
        public Guid           WorkspaceId  { get; init; }
        public Guid?          ProjectId    { get; init; }
        public bool           IsStandalone { get; init; }
        public ActionStatus   Status       { get; init; }
        public ActionPriority Priority     { get; init; }
        public DateTime       DueDate      { get; init; }
        public DateTime       CreatedAt    { get; init; }
        public DateTime       UpdatedAt    { get; init; }
        public bool           IsEscalated  { get; init; }
    }

    private sealed class MilestoneRow
    {
        public Guid            Id             { get; init; }
        public string          Code           { get; init; } = string.Empty;
        public string          Name           { get; init; } = string.Empty;
        public Guid            ProjectId      { get; init; }
        public MilestoneStatus Status         { get; init; }
        public DateTime        PlannedDueDate { get; init; }
        public decimal         Completion     { get; init; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scoping
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Workspace IDs the caller may see; null = unrestricted, empty = none.</summary>
    private async Task<List<Guid>?> VisibleWorkspaceIdsAsync()
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return new List<Guid>();
        return await _workspaceAccess.GetVisibleWorkspaceIdsAsync(userId);
    }

    /// <summary>
    /// WO-2026-11 P-2: projects where the caller is lead PM, sponsor or a project
    /// member. The scoped queries also include these projects (and their action
    /// items) — and nothing else of their workspaces. The counting rules in
    /// <c>DashboardRules</c> are unchanged; this only widens which rows they count.
    /// </summary>
    private async Task<List<Guid>> RoleProjectIdsAsync()
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return new List<Guid>();
        return await _workspaceAccess.GetProjectRoleProjectIdsAsync(userId) ?? new List<Guid>();
    }

    private async Task<List<ActionRow>> LoadActionRowsAsync(IQueryable<ActionItem> query, CancellationToken ct) =>
        await query.AsNoTracking()
            .Select(a => new ActionRow
            {
                Id           = a.Id,
                WorkspaceId  = a.WorkspaceId,
                ProjectId    = a.ProjectId,
                IsStandalone = a.IsStandalone,
                Status       = a.Status,
                Priority     = a.Priority,
                DueDate      = a.DueDate,
                CreatedAt    = a.CreatedAt,
                UpdatedAt    = a.UpdatedAt,
                IsEscalated  = a.IsEscalated,
            })
            .ToListAsync(ct);

    private async Task<List<ActionRow>> LoadVisibleActionRowsAsync(CancellationToken ct)
    {
        var wsIds = await VisibleWorkspaceIdsAsync();
        var roleIds = await RoleProjectIdsAsync();   // WO-2026-11 P-2
        if (wsIds is { Count: 0 } && roleIds.Count == 0) return new List<ActionRow>();

        var query = _db.ActionItems.AsQueryable();
        if (wsIds != null) query = query.Where(a => wsIds.Contains(a.WorkspaceId)
            || (a.ProjectId != null && roleIds.Contains(a.ProjectId.Value)));
        return await LoadActionRowsAsync(query, ct);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Chart helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static List<ChartSliceDto> StatusSlices(IEnumerable<ActionRow> rows) =>
        rows.GroupBy(r => r.Status)
            .OrderBy(g => g.Key)
            .Select(g => new ChartSliceDto
            {
                Key   = ((int)g.Key).ToString(),
                Label = g.Key.GetDescription(),
                Count = g.Count(),
            })
            .ToList();

    /// <summary>Open items by priority, most urgent first; every priority is listed.</summary>
    private static List<ChartSliceDto> PrioritySlices(IEnumerable<ActionRow> rows)
    {
        var open = rows.Where(r => ActionItemRules.IsOpen(r.Status)).ToList();
        return Enum.GetValues<ActionPriority>()
            .OrderByDescending(p => p)
            .Select(p => new ChartSliceDto
            {
                Key   = ((int)p).ToString(),
                Label = p.GetDescription(),
                Count = open.Count(r => r.Priority == p),
            })
            .ToList();
    }

    private static List<ChartSliceDto> DueSlices(IEnumerable<ActionRow> rows, DateTime today)
    {
        var buckets = rows
            .Select(r => ActionItemRules.DueBucket(r.Status, r.DueDate, today))
            .Where(b => b != null)
            .GroupBy(b => b!)
            .ToDictionary(g => g.Key, g => g.Count());

        return ActionItemRules.DueBuckets
            .Select(b => new ChartSliceDto { Key = b.Key, Label = b.Label, Count = buckets.GetValueOrDefault(b.Key) })
            .ToList();
    }

    private static List<ChartSliceDto> AgeSlices(IEnumerable<ActionRow> rows, DateTime today)
    {
        var buckets = rows
            .Select(r => ActionItemRules.AgeBucket(r.Status, r.CreatedAt, today))
            .Where(b => b != null)
            .GroupBy(b => b!)
            .ToDictionary(g => g.Key, g => g.Count());

        return ActionItemRules.AgeBuckets
            .Select(b => new ChartSliceDto { Key = b.Key, Label = b.Label, Count = buckets.GetValueOrDefault(b.Key) })
            .ToList();
    }

    private static decimal Percent(int part, int whole) =>
        whole > 0 ? Math.Round((decimal)part / whole * 100m, 1) : 0m;

    // ─────────────────────────────────────────────────────────────────────────
    // My actions
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<MyActionsDashboardDto> GetMyActionsAsync(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return new MyActionsDashboardDto();

        // Like GET /action-items/my-actions: everything assigned to the caller,
        // whichever workspace it lives in.
        var rows = await LoadActionRowsAsync(
            _db.ActionItems.Where(a => a.Assignees.Any(aa => aa.UserId == userId)), ct);

        var today     = UtcNow.Date;
        var completed = rows.Count(r => r.Status == ActionStatus.Done);
        var due       = DueSlices(rows, today);

        return new MyActionsDashboardDto
        {
            Total          = rows.Count,
            Open           = rows.Count(r => ActionItemRules.IsOpen(r.Status)),
            Completed      = completed,
            Overdue        = rows.Count(r => ActionItemRules.IsOverdue(r.Status, r.DueDate, today)),
            DueThisWeek    = due.First(s => s.Key == ActionItemRules.DueNext7).Count,
            CompletionRate = Percent(completed, rows.Count),
            ByStatus       = StatusSlices(rows),
            ByPriority     = PrioritySlices(rows),
            ByDue          = due,
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Team activity
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<TeamMemberActivityDto>> GetTeamActivityAsync(CancellationToken ct)
    {
        var rows = await LoadVisibleActionRowsAsync(ct);
        return await BuildTeamAsync(rows, ct);
    }

    private async Task<List<TeamMemberActivityDto>> BuildTeamAsync(List<ActionRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return new List<TeamMemberActivityDto>();

        var byId  = rows.ToDictionary(r => r.Id);
        var today = UtcNow.Date;

        // The junction rows are tiny; matching them to the already-scoped items
        // in memory avoids sending a very long ID list to the database.
        var assignments = await _db.ActionItemAssignees.AsNoTracking()
            .Select(aa => new { aa.UserId, aa.ActionItemId })
            .ToListAsync(ct);

        var perUser = assignments
            .Where(a => byId.ContainsKey(a.ActionItemId))
            .GroupBy(a => a.UserId)
            .Select(g =>
            {
                var items = g.Select(a => byId[a.ActionItemId]).ToList();
                var done  = items.Count(i => i.Status == ActionStatus.Done);
                return new TeamMemberActivityDto
                {
                    UserId         = g.Key,
                    Total          = items.Count,
                    Open           = items.Count(i => ActionItemRules.IsOpen(i.Status)),
                    InProgress     = items.Count(i => i.Status == ActionStatus.InProgress),
                    Completed      = done,
                    Overdue        = items.Count(i => ActionItemRules.IsOverdue(i.Status, i.DueDate, today)),
                    CompletionRate = Percent(done, items.Count),
                };
            })
            .ToList();

        var userIds = perUser.Select(p => p.UserId).ToList();
        var names = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToListAsync(ct);
        var nameById = names.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());

        foreach (var member in perUser)
        {
            var name = nameById.GetValueOrDefault(member.UserId);
            member.UserName = string.IsNullOrWhiteSpace(name) ? "Unknown" : name;
        }

        return perUser
            .OrderByDescending(m => m.Total)
            .ThenByDescending(m => m.Open)
            .ThenBy(m => m.UserName)
            .ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Action items
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ActionItemsDashboardDto> GetActionItemsAsync(CancellationToken ct)
    {
        var rows  = await LoadVisibleActionRowsAsync(ct);
        var today = UtcNow.Date;

        var completed  = rows.Count(r => r.Status == ActionStatus.Done);
        // UpdatedAt stands in for the completion time, as elsewhere in the app.
        var doneOnTime = rows.Count(r => r.Status == ActionStatus.Done && r.UpdatedAt <= r.DueDate);

        var workspaceTitles = (await _db.Workspaces.AsNoTracking()
                .Select(w => new { w.Id, w.Title })
                .ToListAsync(ct))
            .ToDictionary(w => w.Id, w => w.Title);

        var byWorkspace = rows
            .GroupBy(r => r.WorkspaceId)
            .Select(g => new WorkspaceActionsDto
            {
                WorkspaceId = g.Key,
                Title       = workspaceTitles.GetValueOrDefault(g.Key, "Unknown workspace"),
                Total       = g.Count(),
                Open        = g.Count(r => ActionItemRules.IsOpen(r.Status)),
                Completed   = g.Count(r => r.Status == ActionStatus.Done),
                Overdue     = g.Count(r => ActionItemRules.IsOverdue(r.Status, r.DueDate, today)),
            })
            .OrderByDescending(w => w.Total)
            .ThenBy(w => w.Title)
            .Take(MaxWorkspaceRows)
            .ToList();

        var dto = new ActionItemsDashboardDto
        {
            Total          = rows.Count,
            Open           = rows.Count(r => ActionItemRules.IsOpen(r.Status)),
            Completed      = completed,
            Overdue        = rows.Count(r => ActionItemRules.IsOverdue(r.Status, r.DueDate, today)),
            Escalated      = rows.Count(r => r.IsEscalated),
            Standalone     = rows.Count(r => r.IsStandalone),
            ProjectLinked  = rows.Count(r => !r.IsStandalone),
            CompletionRate = Percent(completed, rows.Count),
            OnTimeRate     = Percent(doneOnTime, completed),
            ByStatus       = StatusSlices(rows),
            ByPriority     = PrioritySlices(rows),
            ByDue          = DueSlices(rows, today),
            ByAge          = AgeSlices(rows, today),
            Trend          = BuildTrend(rows, today),
            ByWorkspace    = byWorkspace,
            Team           = await BuildTeamAsync(rows, ct),
        };

        _logger.LogInformation("Action-items dashboard built from {Count} visible items", rows.Count);
        return dto;
    }

    /// <summary>Created vs completed for the last <see cref="TrendWeeks"/> weeks (Monday-based).</summary>
    private static List<ActionTrendPointDto> BuildTrend(List<ActionRow> rows, DateTime today)
    {
        static DateTime WeekStart(DateTime d)
        {
            var offset = ((int)d.DayOfWeek + 6) % 7;   // Monday = 0
            return d.Date.AddDays(-offset);
        }

        var thisWeek = WeekStart(today);
        var points   = new List<ActionTrendPointDto>();

        for (var i = TrendWeeks - 1; i >= 0; i--)
        {
            var start = thisWeek.AddDays(-7 * i);
            var end   = start.AddDays(7);
            points.Add(new ActionTrendPointDto
            {
                WeekStart = start,
                Label     = start.ToString("d MMM"),
                Created   = rows.Count(r => r.CreatedAt >= start && r.CreatedAt < end),
                Completed = rows.Count(r =>
                    r.Status == ActionStatus.Done && r.UpdatedAt >= start && r.UpdatedAt < end),
            });
        }

        return points;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Projects
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ProjectsDashboardDto> GetProjectsAsync(CancellationToken ct)
    {
        var today = UtcNow.Date;
        var (projects, milestones) = await BuildProjectRowsAsync(objectiveIds: null, ct);

        var underWay = projects
            .Where(p => p.Status == (int)ProjectStatus.Active || p.Status == (int)ProjectStatus.OnHold)
            .ToList();

        var projectNames = projects.ToDictionary(p => p.Id, p => p.Name);
        var soon         = today.AddDays(MilestoneSoonDays);

        var liveMilestones = milestones.Where(m => m.Status != MilestoneStatus.Cancelled).ToList();
        var openMilestones = liveMilestones.Where(m => m.Status != MilestoneStatus.Completed).ToList();

        var milestoneRows = openMilestones
            .Select(m => new MilestoneRowDto
            {
                Id             = m.Id,
                Code           = m.Code,
                Name           = m.Name,
                ProjectId      = m.ProjectId,
                ProjectName    = projectNames.GetValueOrDefault(m.ProjectId, string.Empty),
                Status         = (int)m.Status,
                StatusLabel    = m.Status.GetDescription(),
                PlannedDueDate = m.PlannedDueDate,
                Completion     = m.Completion,
                IsLate         = ProjectRules.IsMilestoneLate(m.Status, m.PlannedDueDate, today),
                DaysToDue      = (int)(m.PlannedDueDate.Date - today).TotalDays,
            })
            .Where(m => m.IsLate || m.PlannedDueDate < soon)
            .OrderBy(m => m.PlannedDueDate)
            .Take(MaxMilestoneRows)
            .ToList();

        var dto = new ProjectsDashboardDto
        {
            Total       = projects.Count,
            Active      = projects.Count(p => p.Status == (int)ProjectStatus.Active),
            Completed   = projects.Count(p => p.Status == (int)ProjectStatus.Completed),
            OnTrack     = projects.Count(p => p.Health == ProjectRules.HealthOnTrack),
            AtRisk      = projects.Count(p => p.Health == ProjectRules.HealthAtRisk),
            Delayed     = projects.Count(p => p.Health == ProjectRules.HealthDelayed),
            AvgProgress = underWay.Count > 0 ? Math.Round(underWay.Average(p => p.Progress), 1) : 0m,

            MilestonesTotal     = liveMilestones.Count,
            MilestonesCompleted = liveMilestones.Count(m => m.Status == MilestoneStatus.Completed),
            MilestonesDelayed   = openMilestones.Count(m => ProjectRules.IsMilestoneLate(m.Status, m.PlannedDueDate, today)),
            MilestonesDueSoon   = openMilestones.Count(m =>
                !ProjectRules.IsMilestoneLate(m.Status, m.PlannedDueDate, today) && m.PlannedDueDate < soon),

            Projects   = projects,
            Milestones = milestoneRows,
        };

        _logger.LogInformation("Projects dashboard built from {Count} visible projects", projects.Count);
        return dto;
    }

    /// <summary>
    /// Visible projects as dashboard rows, with progress and health worked out
    /// from their action items and milestones. When <paramref name="objectiveIds"/>
    /// is given, only projects linked to one of those strategic objectives.
    /// </summary>
    private async Task<(List<ProjectRowDto> Projects, List<MilestoneRow> Milestones)> BuildProjectRowsAsync(
        List<Guid>? objectiveIds, CancellationToken ct)
    {
        var empty = (new List<ProjectRowDto>(), new List<MilestoneRow>());

        var wsIds = await VisibleWorkspaceIdsAsync();
        var roleIds = await RoleProjectIdsAsync();   // WO-2026-11 P-2
        if (wsIds is { Count: 0 } && roleIds.Count == 0) return empty;
        if (objectiveIds is { Count: 0 }) return empty;

        var query = _db.Projects.AsNoTracking().AsQueryable();
        if (wsIds != null) query = query.Where(p => wsIds.Contains(p.WorkspaceId) || roleIds.Contains(p.Id));
        if (objectiveIds != null)
            query = query.Where(p =>
                p.StrategicObjectiveId.HasValue && objectiveIds.Contains(p.StrategicObjectiveId.Value));

        var projects = await query
            .Select(p => new
            {
                p.Id,
                p.ProjectCode,
                p.Name,
                p.WorkspaceId,
                WorkspaceTitle = p.Workspace.Title,
                p.Status,
                p.ProjectType,
                p.Priority,
                p.StrategicObjectiveId,
                PmFirstName = p.ProjectManager.FirstName,
                PmLastName  = p.ProjectManager.LastName,
                p.PlannedStartDate,
                p.PlannedEndDate,
            })
            .ToListAsync(ct);

        if (projects.Count == 0) return empty;

        var projectIds = projects.Select(p => p.Id).ToHashSet();
        var today      = UtcNow.Date;

        // Project-linked action items and milestones. Both are filtered to the
        // visible projects in memory rather than with a long IN list.
        var actionQuery = _db.ActionItems.Where(a => a.ProjectId != null);
        if (wsIds != null) actionQuery = actionQuery.Where(a => wsIds.Contains(a.WorkspaceId)
            || (a.ProjectId != null && roleIds.Contains(a.ProjectId.Value)));
        var actions = (await LoadActionRowsAsync(actionQuery, ct))
            .Where(a => projectIds.Contains(a.ProjectId!.Value))
            .ToLookup(a => a.ProjectId!.Value);

        var milestones = (await _db.Milestones.AsNoTracking()
                .Select(m => new MilestoneRow
                {
                    Id             = m.Id,
                    Code           = m.MilestoneCode,
                    Name           = m.Name,
                    ProjectId      = m.ProjectId,
                    Status         = m.Status,
                    PlannedDueDate = m.PlannedDueDate,
                    Completion     = m.CompletionPercentage,
                })
                .ToListAsync(ct))
            .Where(m => projectIds.Contains(m.ProjectId))
            .ToList();
        var milestonesByProject = milestones.ToLookup(m => m.ProjectId);

        var rows = projects.Select(p =>
        {
            var items   = actions[p.Id].ToList();
            var live    = milestonesByProject[p.Id].Where(m => m.Status != MilestoneStatus.Cancelled).ToList();
            var done    = items.Count(i => i.Status == ActionStatus.Done);
            var overdue = items.Count(i => ActionItemRules.IsOverdue(i.Status, i.DueDate, today));
            var late    = live.Count(m => ProjectRules.IsMilestoneLate(m.Status, m.PlannedDueDate, today));
            var health  = ProjectRules.Health(p.Status, p.PlannedEndDate, overdue, late, today);

            return new ProjectRowDto
            {
                Id                   = p.Id,
                Code                 = p.ProjectCode,
                Name                 = p.Name,
                WorkspaceId          = p.WorkspaceId,
                WorkspaceTitle       = p.WorkspaceTitle,
                Status               = (int)p.Status,
                StatusLabel          = p.Status.GetDescription(),
                ProjectType          = (int)p.ProjectType,
                TypeLabel            = p.ProjectType.GetDescription(),
                Priority             = (int)p.Priority,
                PriorityLabel        = p.Priority.GetDescription(),
                ProjectManagerName   = $"{p.PmFirstName} {p.PmLastName}".Trim(),
                StrategicObjectiveId = p.StrategicObjectiveId,
                PlannedStartDate     = p.PlannedStartDate,
                PlannedEndDate       = p.PlannedEndDate,
                Progress             = ProjectRules.Progress(
                    p.Status, items.Count, done, live.Select(m => m.Completion).ToList()),
                ActionsTotal         = items.Count,
                ActionsDone          = done,
                ActionsOverdue       = overdue,
                MilestonesTotal      = live.Count,
                MilestonesDone       = live.Count(m => m.Status == MilestoneStatus.Completed),
                MilestonesDelayed    = late,
                Health               = health,
                HealthLabel          = ProjectRules.HealthLabel(health),
                DaysToEnd            = (int)(p.PlannedEndDate.Date - today).TotalDays,
            };
        })
        // Most in need of attention first: delayed, then at risk, then by end date
        .OrderBy(r => r.Health == ProjectRules.HealthDelayed ? 0 : r.Health == ProjectRules.HealthAtRisk ? 1 : 2)
        .ThenBy(r => r.PlannedEndDate)
        .ToList();

        return (rows, milestones);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Strategy
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<StrategyDashboardDto> GetStrategyAsync(int? year, CancellationToken ct)
    {
        var targetYear = year ?? UtcNow.Year;
        var dto        = new StrategyDashboardDto { Year = targetYear };

        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return dto;

        var orgUnitIds = await _strategicScope.GetVisibleOrgUnitIdsAsync(userId, ct);
        if (orgUnitIds is { Count: 0 }) return dto;

        // Plans in scope that are still in play (archived cycles are history).
        var planQuery = _db.StrategicPlans.AsNoTracking()
            .Where(p => p.Status != StrategicPlanStatus.Archived);
        if (orgUnitIds != null) planQuery = planQuery.Where(p => orgUnitIds.Contains(p.OrgUnitId));

        var plans = await planQuery
            .Select(p => new
            {
                p.Id, p.Name, p.OrgUnitId, OrgUnitName = p.OrgUnit.Name,
                p.Status, p.StartYear, p.DurationYears,
            })
            .ToListAsync(ct);

        if (plans.Count == 0) return dto;

        var planIds  = plans.Select(p => p.Id).ToList();
        var planById = plans.ToDictionary(p => p.Id);

        var objectives = await _db.StrategicObjectives.AsNoTracking()
            .Where(o => o.StrategicPlanId != null && planIds.Contains(o.StrategicPlanId.Value))
            .Select(o => new
            {
                o.Id, o.ObjectiveCode, o.Statement, PlanId = o.StrategicPlanId!.Value,
                o.OrgUnitId, OrgUnitName = o.OrgUnit.Name,
            })
            .ToListAsync(ct);

        var objectiveIds  = objectives.Select(o => o.Id).ToList();
        var objectiveById = objectives.ToDictionary(o => o.Id);

        var kpis = await _db.Kpis.AsNoTracking()
            .Where(k => objectiveIds.Contains(k.StrategicObjectiveId))
            .Select(k => new { k.Id, k.KpiNumber, k.Name, k.Unit, k.Period, k.StrategicObjectiveId })
            .ToListAsync(ct);

        var kpiIds = kpis.Select(k => k.Id).ToHashSet();

        // Targets and actuals for the year, matched to the KPIs in memory.
        var targets = (await _db.KpiTargets.AsNoTracking()
                .Where(t => t.Year == targetYear)
                .Select(t => new { t.Id, t.KpiId, t.Target, t.Direction })
                .ToListAsync(ct))
            .Where(t => kpiIds.Contains(t.KpiId))
            .ToList();
        var targetById = targets.ToDictionary(t => t.Id);

        var actuals = (await _db.KpiActuals.AsNoTracking()
                .Where(a => a.Year == targetYear)
                .Select(a => new { a.KpiTargetId, a.ActualValue, a.Status })
                .ToListAsync(ct))
            .Where(a => targetById.ContainsKey(a.KpiTargetId))
            .ToList();

        // Achievement per KPI = mean over its approved actuals that have a usable target.
        var achievementByKpi = actuals
            .Where(a => a.Status == KpiActualStatus.Approved)
            .Select(a =>
            {
                var t = targetById[a.KpiTargetId];
                return new { t.KpiId, Value = KpiAchievement.Compute(a.ActualValue, t.Target, t.Direction) };
            })
            .Where(x => x.Value.HasValue)
            .GroupBy(x => x.KpiId)
            .ToDictionary(g => g.Key, g => (Value: Math.Round(g.Average(x => x.Value!.Value), 1), Count: g.Count()));

        var targetCountByKpi = targets
            .Where(t => t.Target.HasValue)
            .GroupBy(t => t.KpiId)
            .ToDictionary(g => g.Key, g => g.Count());

        var kpiRows = kpis.Select(k =>
        {
            var objective   = objectiveById[k.StrategicObjectiveId];
            var hasData     = achievementByKpi.TryGetValue(k.Id, out var ach);
            decimal? value  = hasData ? ach.Value : null;
            var health      = KpiAchievement.Health(value);

            return new StrategyKpiRowDto
            {
                Id            = k.Id,
                KpiNumber     = k.KpiNumber,
                Name          = k.Name,
                Unit          = k.Unit,
                PeriodLabel   = k.Period.ToString(),
                ObjectiveId   = objective.Id,
                ObjectiveCode = objective.ObjectiveCode,
                PlanId        = objective.PlanId,
                OrgUnitId     = objective.OrgUnitId,
                OrgUnitName   = objective.OrgUnitName,
                TargetCount   = targetCountByKpi.GetValueOrDefault(k.Id),
                ActualCount   = hasData ? ach.Count : 0,
                Achievement   = value,
                Health        = health,
                HealthLabel   = KpiAchievement.HealthLabel(health),
            };
        })
        // Worst first, then those still without data
        .OrderBy(r => r.Achievement.HasValue ? 0 : 1)
        .ThenBy(r => r.Achievement ?? 0m)
        .ThenBy(r => r.ObjectiveCode).ThenBy(r => r.KpiNumber)
        .ToList();

        var (projectRows, _) = await BuildProjectRowsAsync(objectiveIds, ct);
        var projectsByObjective = projectRows
            .Where(p => p.StrategicObjectiveId.HasValue)
            .ToLookup(p => p.StrategicObjectiveId!.Value);
        var kpiRowsByObjective = kpiRows.ToLookup(r => r.ObjectiveId);

        static decimal? MeanAchievement(IEnumerable<StrategyKpiRowDto> rows)
        {
            var values = rows.Where(r => r.Achievement.HasValue).Select(r => r.Achievement!.Value).ToList();
            return values.Count > 0 ? Math.Round(values.Average(), 1) : null;
        }

        var objectiveRows = objectives.Select(o =>
        {
            var oKpis     = kpiRowsByObjective[o.Id].ToList();
            var oProjects = projectsByObjective[o.Id].ToList();
            var plan      = planById[o.PlanId];

            return new StrategyObjectiveRowDto
            {
                Id                 = o.Id,
                Code               = o.ObjectiveCode,
                Statement          = o.Statement,
                PlanId             = o.PlanId,
                PlanName           = plan.Name,
                OrgUnitId          = o.OrgUnitId,
                OrgUnitName        = o.OrgUnitName,
                KpiCount           = oKpis.Count,
                KpisOnTrack        = oKpis.Count(k => k.Health == KpiAchievement.HealthOnTrack),
                KpisAtRisk         = oKpis.Count(k => k.Health == KpiAchievement.HealthAtRisk),
                KpisOffTrack       = oKpis.Count(k => k.Health == KpiAchievement.HealthOffTrack),
                KpisNoData         = oKpis.Count(k => k.Health == KpiAchievement.HealthNoData),
                AvgAchievement     = MeanAchievement(oKpis),
                ProjectCount       = oProjects.Count,
                ProjectsCompleted  = oProjects.Count(p => p.Status == (int)ProjectStatus.Completed),
                AvgProjectProgress = oProjects.Count > 0 ? Math.Round(oProjects.Average(p => p.Progress), 1) : 0m,
            };
        })
        .OrderBy(r => r.PlanName).ThenBy(r => r.Code)
        .ToList();

        var objectivesByPlan = objectiveRows.ToLookup(o => o.PlanId);
        var kpiRowsByPlan    = kpiRows.ToLookup(k => k.PlanId);

        dto.PlanRows = plans.Select(p => new StrategyPlanRowDto
        {
            Id             = p.Id,
            Name           = p.Name,
            OrgUnitId      = p.OrgUnitId,
            OrgUnitName    = p.OrgUnitName,
            Status         = (int)p.Status,
            StatusLabel    = PlanStatusLabel(p.Status),
            StartYear      = p.StartYear,
            EndYear        = p.StartYear + p.DurationYears - 1,
            ObjectiveCount = objectivesByPlan[p.Id].Count(),
            KpiCount       = kpiRowsByPlan[p.Id].Count(),
            AvgAchievement = MeanAchievement(kpiRowsByPlan[p.Id]),
        })
        .OrderBy(r => r.OrgUnitName).ThenBy(r => r.Name)
        .ToList();

        dto.ObjectiveRows = objectiveRows;
        dto.KpiRows       = kpiRows;
        dto.ProjectRows   = projectRows;

        dto.Plans                      = plans.Count;
        dto.Objectives                 = objectives.Count;
        dto.Kpis                       = kpiRows.Count;
        dto.KpisWithData               = kpiRows.Count(k => k.Achievement.HasValue);
        dto.AvgAchievement             = MeanAchievement(kpiRows);
        dto.StrategicProjects          = projectRows.Count;
        dto.StrategicProjectsCompleted = projectRows.Count(p => p.Status == (int)ProjectStatus.Completed);
        dto.ActualsPendingApproval     = actuals.Count(a => a.Status == KpiActualStatus.PendingApproval);

        dto.ActualsByStatus = Enum.GetValues<KpiActualStatus>()
            .Select(s => new ChartSliceDto
            {
                Key   = ((int)s).ToString(),
                Label = ActualStatusLabel(s),
                Count = actuals.Count(a => a.Status == s),
            })
            .ToList();

        _logger.LogInformation(
            "Strategy dashboard for {Year}: {Plans} plans, {Objectives} objectives, {Kpis} KPIs",
            targetYear, plans.Count, objectives.Count, kpiRows.Count);
        return dto;
    }

    private static string PlanStatusLabel(StrategicPlanStatus status) => status switch
    {
        StrategicPlanStatus.PendingApproval => "Pending approval",
        _                                   => status.ToString(),
    };

    private static string ActualStatusLabel(KpiActualStatus status) => status switch
    {
        KpiActualStatus.PendingApproval => "Pending approval",
        _                               => status.ToString(),
    };
}
