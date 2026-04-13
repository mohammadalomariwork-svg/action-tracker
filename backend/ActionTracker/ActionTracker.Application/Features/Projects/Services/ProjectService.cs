using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Extensions;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.Notifications.DTOs;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Features.Projects.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActionTracker.Application.Features.Projects.Services;

public class ProjectService : IProjectService
{
    private readonly IAppDbContext _db;
    private readonly ILogger<ProjectService> _logger;
    private readonly IEmailSender _emailSender;
    private readonly INotificationService _notificationService;
    private readonly AppSettings _appSettings;
    private readonly IServiceScopeFactory _scopeFactory;

    public ProjectService(
        IAppDbContext db,
        ILogger<ProjectService> logger,
        IEmailSender emailSender,
        INotificationService notificationService,
        IOptions<AppSettings> appSettings,
        IServiceScopeFactory scopeFactory)
    {
        _db                  = db;
        _logger              = logger;
        _emailSender         = emailSender;
        _notificationService = notificationService;
        _appSettings         = appSettings.Value;
        _scopeFactory        = scopeFactory;
    }

    public async Task<PagedResult<ProjectResponseDto>> GetAllAsync(ProjectFilterDto filter, CancellationToken ct)
    {
        var query = _db.Projects
            .Include(p => p.Workspace)
            .Include(p => p.ProjectManager)
            .Include(p => p.StrategicObjective)
            .Include(p => p.OwnerOrgUnit)
            .Include(p => p.Sponsors).ThenInclude(s => s.User)
            .AsQueryable();

        if (filter.IncludeDeleted)
            query = query.IgnoreQueryFilters();

        if (filter.WorkspaceId.HasValue)
            query = query.Where(p => p.WorkspaceId == filter.WorkspaceId.Value);

        if (filter.Status.HasValue)
            query = query.Where(p => p.Status == filter.Status.Value);

        if (filter.ProjectType.HasValue)
            query = query.Where(p => p.ProjectType == filter.ProjectType.Value);

        if (filter.Priority.HasValue)
            query = query.Where(p => p.Priority == filter.Priority.Value);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.ProjectCode.ToLower().Contains(term) ||
                (p.Description != null && p.Description.ToLower().Contains(term)));
        }

        // Filter projects whose workspace belongs to a visible org unit.
        if (filter.VisibleOrgUnitIds != null && filter.VisibleOrgUnitIds.Count > 0)
        {
            var ids = filter.VisibleOrgUnitIds;
            query = query.Where(p =>
                _db.Workspaces.Any(w =>
                    w.Id == p.WorkspaceId &&
                    w.OrgUnitId != null &&
                    ids.Contains(w.OrgUnitId.Value)));
        }

        // Sorting
        query = filter.SortBy?.ToLower() switch
        {
            "name"             => filter.SortDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            "projectcode"      => filter.SortDescending ? query.OrderByDescending(p => p.ProjectCode) : query.OrderBy(p => p.ProjectCode),
            "status"           => filter.SortDescending ? query.OrderByDescending(p => p.Status) : query.OrderBy(p => p.Status),
            "priority"         => filter.SortDescending ? query.OrderByDescending(p => p.Priority) : query.OrderBy(p => p.Priority),
            "plannedstartdate" => filter.SortDescending ? query.OrderByDescending(p => p.PlannedStartDate) : query.OrderBy(p => p.PlannedStartDate),
            "plannedenddate"   => filter.SortDescending ? query.OrderByDescending(p => p.PlannedEndDate) : query.OrderBy(p => p.PlannedEndDate),
            _                  => filter.SortDescending ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
        };

        var projected = query.Select(p => new ProjectResponseDto
        {
            Id                          = p.Id,
            ProjectCode                 = p.ProjectCode,
            Name                        = p.Name,
            Description                 = p.Description,
            WorkspaceId                 = p.WorkspaceId,
            WorkspaceTitle              = p.Workspace != null ? p.Workspace.Title : string.Empty,
            ProjectType                 = p.ProjectType,
            Status                      = p.Status,
            Priority                    = p.Priority,
            StrategicObjectiveId        = p.StrategicObjectiveId,
            StrategicObjectiveStatement = p.StrategicObjective != null ? p.StrategicObjective.Statement : null,
            ProjectManagerUserId        = p.ProjectManagerUserId,
            ProjectManagerName          = p.ProjectManager != null ? p.ProjectManager.FirstName + " " + p.ProjectManager.LastName : string.Empty,
            Sponsors                    = p.Sponsors.Select(s => new SponsorDto
            {
                UserId   = s.UserId,
                FullName = s.User != null ? s.User.FirstName + " " + s.User.LastName : string.Empty,
                Email    = s.User != null ? s.User.Email! : string.Empty,
            }).ToList(),
            OwnerOrgUnitId   = p.OwnerOrgUnitId,
            OwnerOrgUnitName = p.OwnerOrgUnit != null ? p.OwnerOrgUnit.Name : null,
            PlannedStartDate = p.PlannedStartDate,
            PlannedEndDate   = p.PlannedEndDate,
            ActualStartDate  = p.ActualStartDate,
            ApprovedBudget   = p.ApprovedBudget,
            Currency         = p.Currency,
            IsBaselined      = p.IsBaselined,
            IsDeleted        = p.IsDeleted,
            ActionItemCount  = _db.ActionItems.Count(a => a.ProjectId == p.Id),
            CreatedAt        = p.CreatedAt,
            UpdatedAt        = p.UpdatedAt,
        });

        return await PagedResult<ProjectResponseDto>.CreateAsync(projected, filter.PageNumber, filter.PageSize, ct);
    }

    public async Task<ProjectResponseDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var project = await _db.Projects
            .Include(p => p.Workspace)
            .Include(p => p.ProjectManager)
            .Include(p => p.StrategicObjective)
            .Include(p => p.OwnerOrgUnit)
            .Include(p => p.Sponsors).ThenInclude(s => s.User)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project is null) return null;
        var dto = MapToDto(project);
        dto.ActionItemCount = await _db.ActionItems.CountAsync(a => a.ProjectId == id, ct);
        return dto;
    }

    public async Task<ProjectResponseDto> CreateAsync(ProjectCreateDto dto, string userId, CancellationToken ct)
    {
        // Validate strategic objective requirement
        if (dto.ProjectType == ProjectType.Strategic && !dto.StrategicObjectiveId.HasValue)
            throw new ArgumentException("Strategic objective is required for strategic projects.");

        if (dto.SponsorUserIds.Count == 0)
            throw new ArgumentException("At least one sponsor is required.");

        // Generate project code: PRJ-{year}-{sequence}
        var year = DateTime.UtcNow.Year;
        var count = await _db.Projects
            .IgnoreQueryFilters()
            .CountAsync(p => p.ProjectCode.StartsWith($"PRJ-{year}-"), ct);
        var projectCode = $"PRJ-{year}-{(count + 1):D3}";

        var project = new Project
        {
            Id                    = Guid.NewGuid(),
            ProjectCode           = projectCode,
            Name                  = dto.Name.Trim(),
            Description           = dto.Description?.Trim(),
            WorkspaceId           = dto.WorkspaceId,
            ProjectType           = dto.ProjectType,
            Status                = ProjectStatus.Draft,
            StrategicObjectiveId  = dto.ProjectType == ProjectType.Strategic ? dto.StrategicObjectiveId : null,
            Priority              = dto.Priority,
            ProjectManagerUserId  = dto.ProjectManagerUserId,
            OwnerOrgUnitId        = dto.OwnerOrgUnitId,
            PlannedStartDate      = dto.PlannedStartDate,
            PlannedEndDate        = dto.PlannedEndDate,
            ApprovedBudget        = dto.ApprovedBudget,
            Currency              = "AED",
            IsBaselined           = false,
            CreatedBy             = userId,
            CreatedAt             = DateTime.UtcNow,
        };

        _db.Projects.Add(project);

        // Add sponsors
        foreach (var sponsorId in dto.SponsorUserIds.Distinct())
        {
            _db.ProjectSponsors.Add(new ProjectSponsor
            {
                ProjectId = project.Id,
                UserId    = sponsorId,
            });
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Project {ProjectCode} created by user {UserId}", projectCode, userId);

        // Fire-and-forget email notification
        var capturedProject = project;
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            try
            {
                var full = await db.Projects
                    .Include(p => p.Workspace)
                    .Include(p => p.ProjectManager)
                    .FirstOrDefaultAsync(p => p.Id == capturedProject.Id);
                if (full is null) return;

                var placeholders = BuildProjectPlaceholders(full);
                var pmEmail = string.IsNullOrWhiteSpace(full.ProjectManagerUserId)
                    ? null
                    : await db.Users
                        .Where(u => u.Id == full.ProjectManagerUserId && u.IsActive)
                        .Select(u => u.Email)
                        .FirstOrDefaultAsync();
                if (pmEmail is not null)
                {
                    await emailSender.SendEmailAsync("Project.Created", placeholders,
                        [pmEmail], "Project", full.Id, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email for Project.Created {ProjectCode}", capturedProject.ProjectCode);
            }

            // In-app notification — notify PM (exclude actor)
            try
            {
                if (capturedProject.ProjectManagerUserId != userId)
                {
                    var actorName = await db.Users
                        .Where(u => u.Id == userId)
                        .Select(u => u.FirstName + " " + u.LastName)
                        .FirstOrDefaultAsync();
                    await notifService.CreateAsync(new CreateNotificationDto
                    {
                        UserId               = capturedProject.ProjectManagerUserId,
                        Title                = "New Project",
                        Message              = $"Project {capturedProject.Name} ({capturedProject.ProjectCode}) has been created",
                        Type                 = "Project",
                        ActionType           = "Created",
                        RelatedEntityType    = "Project",
                        RelatedEntityId      = capturedProject.Id,
                        RelatedEntityCode    = capturedProject.ProjectCode,
                        Url                  = $"{_appSettings.FrontendBaseUrl}/projects/{capturedProject.Id}",
                        CreatedByUserId      = userId,
                        CreatedByDisplayName = actorName,
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification for Project.Created {ProjectCode}", capturedProject.ProjectCode);
            }
        });

        return (await GetByIdAsync(project.Id, ct))!;
    }

    public async Task<ProjectResponseDto> UpdateAsync(Guid id, ProjectUpdateDto dto, CancellationToken ct)
    {
        var project = await _db.Projects
            .Include(p => p.Sponsors)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException($"Project {id} not found.");

        var previousStatus = project.Status;

        if (dto.ProjectType == ProjectType.Strategic && !dto.StrategicObjectiveId.HasValue)
            throw new ArgumentException("Strategic objective is required for strategic projects.");

        if (dto.SponsorUserIds.Count == 0)
            throw new ArgumentException("At least one sponsor is required.");

        // Block status changes from PendingApproval via normal update — only workflow review can do that
        if (project.Status == ProjectStatus.PendingApproval && dto.Status != ProjectStatus.PendingApproval)
            throw new ArgumentException("Project status can only be changed from PendingApproval through the approval workflow.");

        // Date freeze: block date changes when project is not in Draft
        if (project.Status != ProjectStatus.Draft)
        {
            if (dto.PlannedStartDate != project.PlannedStartDate || dto.PlannedEndDate != project.PlannedEndDate)
                throw new ArgumentException("Project dates cannot be changed after the project has been submitted for approval or activated.");
        }

        // Validate milestones/phases/action items when transitioning to Active or Completed
        if ((dto.Status == ProjectStatus.Active || dto.Status == ProjectStatus.Completed)
            && project.Status != dto.Status)
        {
            await ValidateProjectMilestonesAndActionsAsync(id, dto.Status, ct);
        }

        // Set actual start date when transitioning to Active
        if (dto.Status == ProjectStatus.Active && project.Status == ProjectStatus.Draft && !dto.ActualStartDate.HasValue)
            dto.ActualStartDate = DateTime.UtcNow;

        project.Name                  = dto.Name.Trim();
        project.Description           = dto.Description?.Trim();
        project.ProjectType           = dto.ProjectType;
        project.Status                = dto.Status;
        project.StrategicObjectiveId  = dto.ProjectType == ProjectType.Strategic ? dto.StrategicObjectiveId : null;
        project.Priority              = dto.Priority;
        project.ProjectManagerUserId  = dto.ProjectManagerUserId;
        project.OwnerOrgUnitId        = dto.OwnerOrgUnitId;
        project.PlannedStartDate      = dto.PlannedStartDate;
        project.PlannedEndDate        = dto.PlannedEndDate;
        project.ActualStartDate       = dto.ActualStartDate;
        project.ApprovedBudget        = dto.ApprovedBudget;

        // Sync sponsors
        var existingSponsorIds = project.Sponsors.Select(s => s.UserId).ToHashSet();
        var newSponsorIds = dto.SponsorUserIds.Distinct().ToHashSet();

        // Remove sponsors no longer in list
        foreach (var removed in project.Sponsors.Where(s => !newSponsorIds.Contains(s.UserId)).ToList())
            _db.ProjectSponsors.Remove(removed);

        // Add new sponsors
        foreach (var addId in newSponsorIds.Where(id2 => !existingSponsorIds.Contains(id2)))
        {
            _db.ProjectSponsors.Add(new ProjectSponsor
            {
                ProjectId = project.Id,
                UserId    = addId,
            });
        }

        await _db.SaveChangesAsync(ct);

        // Fire-and-forget email if status changed
        if (dto.Status != previousStatus)
        {
            var capturedId = project.Id;
            var capturedNewStatus = dto.Status;
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

                try
                {
                    var full = await db.Projects
                        .Include(p => p.Workspace)
                        .Include(p => p.ProjectManager)
                        .Include(p => p.Sponsors)
                        .FirstOrDefaultAsync(p => p.Id == capturedId);
                    if (full is null) return;

                    var placeholders = BuildProjectPlaceholders(full);

                    // Get recipient emails using scoped db
                    var recipientUserIds = new HashSet<string> { full.ProjectManagerUserId };
                    foreach (var s in full.Sponsors) recipientUserIds.Add(s.UserId);
                    var ids = recipientUserIds.Where(rid => !string.IsNullOrWhiteSpace(rid)).Distinct().ToList();
                    var recipients = ids.Count > 0
                        ? await db.Users
                            .Where(u => ids.Contains(u.Id) && u.IsActive && u.Email != null)
                            .Select(u => u.Email!)
                            .Distinct()
                            .ToListAsync()
                        : new List<string>();

                    await emailSender.SendEmailAsync("Project.StatusChanged", placeholders,
                        recipients, "Project", full.Id);

                    if (capturedNewStatus == ProjectStatus.Completed)
                    {
                        await emailSender.SendEmailAsync("Project.Completed", placeholders,
                            recipients, "Project", full.Id);
                    }

                    // In-app notifications
                    var actorName = await db.Users
                        .Where(u => u.Id == full.CreatedBy)
                        .Select(u => u.FirstName + " " + u.LastName)
                        .FirstOrDefaultAsync();

                    string notifTitle, notifActionType;
                    if (capturedNewStatus == ProjectStatus.Completed)
                    { notifTitle = "Project Completed"; notifActionType = "Completed"; }
                    else
                    { notifTitle = "Project Status Updated"; notifActionType = "StatusChanged"; }

                    var notifications = recipientUserIds
                        .Select(uid => new CreateNotificationDto
                        {
                            UserId               = uid,
                            Title                = notifTitle,
                            Message              = $"{full.ProjectCode} status changed to {capturedNewStatus}",
                            Type                 = "Project",
                            ActionType           = notifActionType,
                            RelatedEntityType    = "Project",
                            RelatedEntityId      = full.Id,
                            RelatedEntityCode    = full.ProjectCode,
                            Url                  = $"{_appSettings.FrontendBaseUrl}/projects/{full.Id}",
                            CreatedByUserId      = full.CreatedBy,
                            CreatedByDisplayName = actorName,
                        }).ToList();
                    if (notifications.Count > 0)
                        await notifService.CreateBulkAsync(notifications);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending status change notifications for Project {Id}", capturedId);
                }
            });
        }

        return (await GetByIdAsync(project.Id, ct))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException($"Project {id} not found.");

        project.IsDeleted = true;

        var milestones = await _db.Milestones
            .Where(m => m.ProjectId == id && !m.IsDeleted)
            .ToListAsync(ct);
        foreach (var m in milestones) m.IsDeleted = true;

        var actionItems = await _db.ActionItems
            .Where(a => a.ProjectId == id && !a.IsDeleted)
            .ToListAsync(ct);
        foreach (var a in actionItems) a.IsDeleted = true;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Project {ProjectCode} soft-deleted with {MilestoneCount} milestones and {ActionCount} action items",
            project.ProjectCode, milestones.Count, actionItems.Count);
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct)
    {
        var project = await _db.Projects
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException($"Project {id} not found.");

        if (!project.IsDeleted)
            throw new InvalidOperationException("Project is not deleted.");

        project.IsDeleted = false;

        var milestones = await _db.Milestones
            .IgnoreQueryFilters()
            .Where(m => m.ProjectId == id && m.IsDeleted)
            .ToListAsync(ct);
        foreach (var m in milestones) m.IsDeleted = false;

        var actionItems = await _db.ActionItems
            .IgnoreQueryFilters()
            .Where(a => a.ProjectId == id && a.IsDeleted)
            .ToListAsync(ct);
        foreach (var a in actionItems) a.IsDeleted = false;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Project {ProjectCode} restored with {MilestoneCount} milestones and {ActionCount} action items",
            project.ProjectCode, milestones.Count, actionItems.Count);
    }

    public async Task<List<StrategicObjectiveOptionDto>> GetStrategicObjectivesForWorkspaceAsync(
        Guid workspaceId, CancellationToken ct)
    {
        var workspace = await _db.Workspaces.FirstOrDefaultAsync(w => w.Id == workspaceId, ct)
            ?? throw new KeyNotFoundException($"Workspace {workspaceId} not found.");

        // Find the OrgUnit matching the workspace's OrganizationUnit name
        var orgUnit = await _db.OrgUnits
            .FirstOrDefaultAsync(o => o.Name == workspace.OrganizationUnit, ct);

        if (orgUnit is null)
            return new List<StrategicObjectiveOptionDto>();

        // Walk up the org unit hierarchy until we find strategic objectives
        var currentOrgUnitId = orgUnit.Id;
        Guid? currentParentId = orgUnit.ParentId;

        while (true)
        {
            var objectives = await _db.StrategicObjectives
                .Where(so => so.OrgUnitId == currentOrgUnitId)
                .OrderBy(so => so.ObjectiveCode)
                .Select(so => new StrategicObjectiveOptionDto
                {
                    Id            = so.Id,
                    ObjectiveCode = so.ObjectiveCode,
                    Statement     = so.Statement,
                })
                .ToListAsync(ct);

            if (objectives.Count > 0)
                return objectives;

            // No objectives found — try parent
            if (!currentParentId.HasValue)
                return new List<StrategicObjectiveOptionDto>();

            var parent = await _db.OrgUnits
                .FirstOrDefaultAsync(o => o.Id == currentParentId.Value, ct);

            if (parent is null)
                return new List<StrategicObjectiveOptionDto>();

            currentOrgUnitId = parent.Id;
            currentParentId = parent.ParentId;
        }
    }

    public async Task<ProjectStatsDto> GetStatsAsync(Guid projectId, CancellationToken ct)
    {
        var milestoneCount = await _db.Milestones
            .CountAsync(m => m.ProjectId == projectId, ct);

        var items = await _db.ActionItems
            .Where(a => a.ProjectId == projectId)
            .Select(a => new { a.Status, a.IsEscalated, a.DueDate, a.UpdatedAt })
            .ToListAsync(ct);

        int total      = items.Count;
        int done       = items.Count(a => a.Status == ActionStatus.Done);
        int doneOnTime = items.Count(a =>
            a.Status == ActionStatus.Done && a.UpdatedAt <= a.DueDate);

        return new ProjectStatsDto
        {
            MilestoneCount  = milestoneCount,
            ActionItemCount = total,
            CompletionRate  = total > 0 ? Math.Round((decimal)done / total * 100, 1) : 0,
            OnTimeRate      = done  > 0 ? Math.Round((decimal)doneOnTime / done * 100, 1) : 0,
            EscalatedCount  = items.Count(a => a.IsEscalated),
        };
    }

    // ── Email helpers ──────────────────────────────────────────

    private Dictionary<string, string> BuildProjectPlaceholders(Project p) => new()
    {
        ["ProjectCode"]     = p.ProjectCode,
        ["ProjectName"]     = p.Name,
        ["Description"]     = p.Description ?? string.Empty,
        ["Status"]          = p.Status.ToString(),
        ["Priority"]        = p.Priority.ToString(),
        ["ProjectManager"]  = p.ProjectManager != null ? $"{p.ProjectManager.FirstName} {p.ProjectManager.LastName}".Trim() : string.Empty,
        ["WorkspaceName"]   = p.Workspace?.Title ?? string.Empty,
        ["PlannedStartDate"]= p.PlannedStartDate.ToString("yyyy-MM-dd"),
        ["PlannedEndDate"]  = p.PlannedEndDate.ToString("yyyy-MM-dd"),
        ["Budget"]          = p.ApprovedBudget?.ToString("N2") ?? string.Empty,
        ["ItemUrl"]         = $"{_appSettings.FrontendBaseUrl}/projects/{p.Id}",
    };

    private async Task<List<string>> GetProjectRecipientEmailsAsync(Project project)
    {
        var userIds = new HashSet<string> { project.ProjectManagerUserId };
        foreach (var s in project.Sponsors)
            userIds.Add(s.UserId);
        return await GetActiveUserEmailsAsync(userIds);
    }

    private async Task<string?> GetUserEmailAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        return await _db.Users
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();
    }

    private async Task<List<string>> GetActiveUserEmailsAsync(IEnumerable<string> userIds)
    {
        var ids = userIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (ids.Count == 0) return [];
        return await _db.Users
            .Where(u => ids.Contains(u.Id) && u.IsActive && u.Email != null)
            .Select(u => u.Email!)
            .Distinct()
            .ToListAsync();
    }

    private async Task<string?> GetUserDisplayNameAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        return await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.FirstName + " " + u.LastName)
            .FirstOrDefaultAsync();
    }

    // ── Mapping helpers ─────────────────────────────────────
    private async Task ValidateProjectMilestonesAndActionsAsync(Guid projectId, ProjectStatus targetStatus, CancellationToken ct)
    {
        var milestones = await _db.Milestones
            .Where(m => m.ProjectId == projectId)
            .Select(m => new { m.Id, m.Phase, m.Name })
            .ToListAsync(ct);

        // All 5 phases must be covered
        var allPhases = Enum.GetValues<ProjectPhase>();
        var coveredPhases = milestones.Select(m => m.Phase).Distinct().ToHashSet();
        var missingPhases = allPhases.Where(p => !coveredPhases.Contains(p)).ToList();
        if (missingPhases.Count > 0)
        {
            var missing = string.Join(", ", missingPhases.Select(p => p.GetDescription()));
            throw new ArgumentException($"The project must have at least one milestone in each phase. Missing phases: {missing}.");
        }

        // Every milestone must have at least one action item
        var milestoneIds = milestones.Select(m => m.Id).ToList();
        var milestonesWithActions = await _db.ActionItems
            .Where(a => a.MilestoneId != null && milestoneIds.Contains(a.MilestoneId.Value))
            .Select(a => a.MilestoneId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var empty = milestones.Where(m => !milestonesWithActions.Contains(m.Id)).ToList();
        if (empty.Count > 0)
        {
            var names = string.Join(", ", empty.Select(m => m.Name));
            throw new ArgumentException($"Every milestone must have at least one action item. Milestones without action items: {names}.");
        }

        // When completing a project, all action items must be Done or Cancelled
        if (targetStatus == ProjectStatus.Completed)
        {
            var incompleteActions = await _db.ActionItems
                .Where(a => a.ProjectId == projectId
                         && !a.IsDeleted
                         && a.Status != ActionStatus.Done
                         && a.Status != ActionStatus.Cancelled)
                .Select(a => a.Title)
                .ToListAsync(ct);

            if (incompleteActions.Count > 0)
            {
                var names = string.Join(", ", incompleteActions);
                throw new ArgumentException(
                    $"Cannot complete the project: all action items must be Done or Cancelled. " +
                    $"Incomplete action items: {names}.");
            }
        }
    }

    private static ProjectResponseDto MapToDto(Project p)
    {
        return new ProjectResponseDto
        {
            Id                          = p.Id,
            ProjectCode                 = p.ProjectCode,
            Name                        = p.Name,
            Description                 = p.Description,
            WorkspaceId                 = p.WorkspaceId,
            WorkspaceTitle              = p.Workspace?.Title ?? string.Empty,
            ProjectType                 = p.ProjectType,
            Status                      = p.Status,
            Priority                    = p.Priority,
            StrategicObjectiveId        = p.StrategicObjectiveId,
            StrategicObjectiveStatement = p.StrategicObjective?.Statement,
            ProjectManagerUserId        = p.ProjectManagerUserId,
            ProjectManagerName          = p.ProjectManager?.FullName ?? string.Empty,
            Sponsors                    = p.Sponsors.Select(s => new SponsorDto
            {
                UserId   = s.UserId,
                FullName = s.User?.FullName ?? string.Empty,
                Email    = s.User?.Email ?? string.Empty,
            }).ToList(),
            OwnerOrgUnitId   = p.OwnerOrgUnitId,
            OwnerOrgUnitName = p.OwnerOrgUnit?.Name,
            PlannedStartDate = p.PlannedStartDate,
            PlannedEndDate   = p.PlannedEndDate,
            ActualStartDate  = p.ActualStartDate,
            ApprovedBudget   = p.ApprovedBudget,
            Currency         = p.Currency,
            IsBaselined      = p.IsBaselined,
            IsDeleted        = p.IsDeleted,
            CreatedAt        = p.CreatedAt,
            UpdatedAt        = p.UpdatedAt,
        };
    }

}
