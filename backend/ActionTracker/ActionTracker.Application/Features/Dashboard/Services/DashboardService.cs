using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.ActionItems.Mappers;
using ActionTracker.Application.Features.Dashboard.DTOs;
using ActionTracker.Application.Features.Dashboard.Interfaces;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Features.Dashboard.Services;

public class DashboardService : IDashboardService
{
    private readonly IAppDbContext _dbContext;
    private readonly ILogger<DashboardService> _logger;

    // Hex colours per status (matches common UI frameworks)
    private static readonly Dictionary<ActionStatus, string> StatusColors = new()
    {
        [ActionStatus.ToDo]       = "#6B7280",  // gray-500
        [ActionStatus.InProgress] = "#3B82F6",  // blue-500
        [ActionStatus.InReview]   = "#F59E0B",  // amber-500
        [ActionStatus.Done]       = "#10B981",  // green-500
        [ActionStatus.Overdue]    = "#EF4444",  // red-500
    };

    public DashboardService(IAppDbContext dbContext, ILogger<DashboardService> logger)
    {
        _dbContext = dbContext;
        _logger    = logger;
    }

    // -------------------------------------------------------------------------
    // KPIs  — single projection query, all aggregation in memory
    // -------------------------------------------------------------------------

    public async Task<DashboardKpiDto> GetKpisAsync(CancellationToken ct)
    {
        // One DB round-trip: pull only the columns needed for every KPI
        var items = await _dbContext.ActionItems
            .Select(a => new
            {
                a.Status,
                a.Priority,
                a.IsEscalated,
                a.DueDate,
                a.UpdatedAt,
            })
            .ToListAsync(ct);

        int total      = items.Count;
        int done       = items.Count(a => a.Status == ActionStatus.Done);
        int doneOnTime = items.Count(a =>
            a.Status == ActionStatus.Done && a.UpdatedAt <= a.DueDate);

        _logger.LogInformation("KPI query returned {Total} action items", total);

        return new DashboardKpiDto
        {
            TotalActions       = total,
            CompletionRate     = total > 0 ? Math.Round((decimal)done / total * 100, 1) : 0,
            OnTimeDeliveryRate = done  > 0 ? Math.Round((decimal)doneOnTime / done * 100, 1) : 0,
            ActiveEscalations  = items.Count(a => a.IsEscalated),
            TeamVelocity       = done,
            CriticalHighCount  = items.Count(a =>
                (a.Priority == ActionPriority.Critical || a.Priority == ActionPriority.High)
                && a.Status != ActionStatus.Done),
            InProgressCount = items.Count(a => a.Status == ActionStatus.InProgress),
            OverdueCount    = items.Count(a => a.Status == ActionStatus.Overdue),
        };
    }

    // -------------------------------------------------------------------------
    // Status breakdown  — single grouped query
    // -------------------------------------------------------------------------

    public async Task<List<StatusBreakdownDto>> GetStatusBreakdownAsync(CancellationToken ct)
    {
        var groups = await _dbContext.ActionItems
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int total = groups.Sum(g => g.Count);

        var breakdown = groups
            .OrderBy(g => g.Status)
            .Select(g => new StatusBreakdownDto
            {
                Status     = g.Status.ToString(),
                Count      = g.Count,
                Percentage = total > 0
                    ? Math.Round((decimal)g.Count / total * 100, 1)
                    : 0,
                Color = StatusColors.GetValueOrDefault(g.Status, "#6B7280"),
            })
            .ToList();

        _logger.LogInformation("Status breakdown: {Count} groups", breakdown.Count);
        return breakdown;
    }

    // -------------------------------------------------------------------------
    // Team workload  — two queries, joined in memory
    // -------------------------------------------------------------------------

    public async Task<List<TeamWorkloadDto>> GetTeamWorkloadAsync(CancellationToken ct)
    {
        // Query 1: aggregate per assignee
        var stats = await _dbContext.ActionItems
            .GroupBy(a => a.AssigneeId)
            .Select(g => new
            {
                AssigneeId     = g.Key,
                AssignedCount  = g.Count(),
                CompletedCount = g.Count(a => a.Status == ActionStatus.Done),
                OverdueCount   = g.Count(a => a.Status == ActionStatus.Overdue),
            })
            .ToListAsync(ct);

        // Query 2: user names for the assignee IDs found above
        var userIds = stats.Select(s => s.AssigneeId).ToList();
        var users = await _dbContext.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToListAsync(ct);

        var userMap = users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}");

        var workload = stats
            .OrderByDescending(s => s.AssignedCount)
            .Select(s => new TeamWorkloadDto
            {
                UserId               = s.AssigneeId,
                UserName             = userMap.GetValueOrDefault(s.AssigneeId, "Unknown"),
                AssignedCount        = s.AssignedCount,
                CompletedCount       = s.CompletedCount,
                OverdueCount         = s.OverdueCount,
                CompletionPercentage = s.AssignedCount > 0
                    ? Math.Round((decimal)s.CompletedCount / s.AssignedCount * 100, 1)
                    : 0,
            })
            .ToList();

        _logger.LogInformation("Team workload: {Count} team members", workload.Count);
        return workload;
    }

    // -------------------------------------------------------------------------
    // Management dashboard  — reuses the above methods + 3 focused queries
    // -------------------------------------------------------------------------

    public async Task<ManagementDashboardDto> GetManagementDashboardAsync(CancellationToken ct)
    {
        // Reuse individual methods for KPIs, breakdown, and workload
        var kpis      = await GetKpisAsync(ct);
        var breakdown = await GetStatusBreakdownAsync(ct);
        var workload  = await GetTeamWorkloadAsync(ct);

        var now = DateTime.UtcNow;

        // At-risk: Overdue or due within 3 days, ordered by urgency, max 5
        var atRiskEntities = await _dbContext.ActionItems
            .Include(a => a.Assignee)
            .Where(a =>
                a.Status != ActionStatus.Done &&
                (a.Status == ActionStatus.Overdue || a.DueDate <= now.AddDays(3)))
            .OrderBy(a => a.DueDate)
            .Take(5)
            .ToListAsync(ct);

        var atRiskItems = atRiskEntities.Select(a =>
        {
            var daysOverdue = a.DueDate < now
                ? (int)(now.Date - a.DueDate.Date).TotalDays
                : 0;

            var severity = (a.Priority == ActionPriority.Critical || a.IsEscalated)
                ? "Critical"
                : a.Priority == ActionPriority.High || a.Status == ActionStatus.Overdue
                    ? "High"
                    : "Medium";

            return new AtRiskItemDto
            {
                Id           = a.Id,
                ActionId     = a.ActionId,
                Title        = a.Title,
                AssigneeName = a.Assignee?.FullName ?? string.Empty,
                Priority     = a.Priority.ToString(),
                Status       = a.Status.ToString(),
                DueDate      = a.DueDate,
                DaysOverdue  = daysOverdue,
                IsEscalated  = a.IsEscalated,
                SeverityLevel = severity,
            };
        }).ToList();

        // Recent activity: last 5 created items
        var recentEntities = await _dbContext.ActionItems
            .Include(a => a.Assignee)
            .OrderByDescending(a => a.CreatedAt)
            .Take(5)
            .ToListAsync(ct);

        var recentActivity = recentEntities.Select(a => new RecentActivityDto
        {
            Id           = a.Id,
            ActionId     = a.ActionId,
            Title        = a.Title,
            AssigneeName = a.Assignee?.FullName ?? string.Empty,
            CreatedAt    = a.CreatedAt,
            Status       = a.Status.ToString(),
        }).ToList();

        // Critical actions: Critical or High priority, not Done
        var criticalEntities = await _dbContext.ActionItems
            .Include(a => a.Assignee)
            .Where(a =>
                (a.Priority == ActionPriority.Critical || a.Priority == ActionPriority.High)
                && a.Status != ActionStatus.Done)
            .OrderByDescending(a => a.Priority)
            .ThenBy(a => a.DueDate)
            .ToListAsync(ct);

        var criticalActions = criticalEntities
            .Select(ActionItemMapper.ToDto)
            .ToList();

        _logger.LogInformation(
            "Management dashboard assembled: {AtRisk} at-risk, {Critical} critical actions",
            atRiskItems.Count, criticalActions.Count);

        return new ManagementDashboardDto
        {
            Kpis            = kpis,
            StatusBreakdown = breakdown,
            TeamWorkload    = workload,
            AtRiskItems     = atRiskItems,
            RecentActivity  = recentActivity,
            CriticalActions = criticalActions,
        };
    }
}
