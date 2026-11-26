using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Extensions;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ActionItemSubItems;
using ActionTracker.Application.Features.Calendars;
using ActionTracker.Application.Features.Calendars.Interfaces;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.Notifications.DTOs;
using ActionTracker.Application.Features.ProjectClassifications.Interfaces;
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
    private readonly IWorkCalendarService _calendars;
    private readonly IProjectClassificationService _classifications;

    public ProjectService(
        IAppDbContext db,
        ILogger<ProjectService> logger,
        IEmailSender emailSender,
        INotificationService notificationService,
        IOptions<AppSettings> appSettings,
        IServiceScopeFactory scopeFactory,
        IWorkCalendarService calendars,
        IProjectClassificationService classifications)
    {
        _db                  = db;
        _logger              = logger;
        _emailSender         = emailSender;
        _notificationService = notificationService;
        _appSettings         = appSettings.Value;
        _scopeFactory        = scopeFactory;
        _calendars           = calendars;
        _classifications     = classifications;
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
            query = query.IgnoreQueryFilters()
                .WhereWorkspaceVisibleTo(_db.CurrentUserId);

        if (filter.WorkspaceId.HasValue)
            query = query.Where(p => p.WorkspaceId == filter.WorkspaceId.Value);

        if (filter.Status.HasValue)
            query = query.Where(p => p.Status == filter.Status.Value);

        if (filter.ProjectType.HasValue)
            query = query.Where(p => p.ProjectType == filter.ProjectType.Value);

        if (filter.Priority.HasValue)
            query = query.Where(p => p.Priority == filter.Priority.Value);

        // Internal classification (WO-2026-11 P-1): by id only, on top of the
        // project filters, so a name never crosses workspaces.
        if (filter.InternalClassificationId.HasValue)
        {
            var classificationId = filter.InternalClassificationId.Value;
            query = query.Where(p => p.InternalClassificationId == classificationId);
        }

        if (filter.Unclassified)
            query = query.Where(p => p.InternalClassificationId == null);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.ProjectCode.ToLower().Contains(term) ||
                (p.Description != null && p.Description.ToLower().Contains(term)));
        }

        if (filter.StrategicObjectiveIds != null && filter.StrategicObjectiveIds.Count > 0)
        {
            var objIds = filter.StrategicObjectiveIds;
            query = query.Where(p =>
                p.StrategicObjectiveId.HasValue &&
                objIds.Contains(p.StrategicObjectiveId.Value));
        }

        // Workspace-membership scoping: regular users only see projects in workspaces
        // they're a member/admin of. Privileged roles pass null (no restriction).
        if (filter.VisibleWorkspaceIds != null)
        {
            var wsIds = filter.VisibleWorkspaceIds;
            if (wsIds.Count == 0)
                query = query.Where(p => false);
            else
                query = query.Where(p => wsIds.Contains(p.WorkspaceId));
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
            // Only a live classification of the project's own workspace is shown
            // (the query filter also hides private ones; this guard covers the
            // include-deleted path, which ignores query filters).
            InternalClassificationId    = p.InternalClassification != null && !p.InternalClassification.IsDeleted
                                          && p.InternalClassification.WorkspaceId == p.WorkspaceId
                                              ? p.InternalClassificationId : null,
            InternalClassificationName  = p.InternalClassification != null && !p.InternalClassification.IsDeleted
                                          && p.InternalClassification.WorkspaceId == p.WorkspaceId
                                              ? p.InternalClassification.Name : null,
            InternalClassificationIsActive = p.InternalClassification != null && !p.InternalClassification.IsDeleted
                                          && p.InternalClassification.WorkspaceId == p.WorkspaceId
                                              ? p.InternalClassification.IsActive : (bool?)null,
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
            .Include(p => p.InternalClassification)
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

        // Gate: PM must be on the workspace's project-manager list.
        if (string.IsNullOrWhiteSpace(dto.ProjectManagerUserId))
            throw new ArgumentException("Project manager is required.");

        var pmAllowed = await _db.WorkspaceProjectManagers
            .AnyAsync(p => p.WorkspaceId == dto.WorkspaceId
                        && p.UserId      == dto.ProjectManagerUserId, ct);
        if (!pmAllowed)
            throw new ArgumentException(
                "The selected user is not on this workspace's Project Managers list. " +
                "Add them on the workspace settings page first.");

        // ── Working-day rule (WO-2026-11 C-3) ─────────────────────────────────
        await _calendars.EnsureWorkingDaysAsync(dto.WorkspaceId, new[]
        {
            WorkingDateCheck.New("Planned start date", dto.PlannedStartDate),
            WorkingDateCheck.New("Planned end date",   dto.PlannedEndDate),
        }, ct);

        // ── Internal classification (WO-2026-11 P-1): optional, an active one of this workspace.
        Guid? internalClassificationId = dto.InternalClassificationId is { } requested && requested != Guid.Empty
            ? requested : null;
        if (internalClassificationId.HasValue)
            await _classifications.EnsureAssignableAsync(dto.WorkspaceId, internalClassificationId.Value, null, ct);

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
            InternalClassificationId = internalClassificationId,
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

        // Gate: PM must be on the workspace's project-manager list (only when it changes).
        if (string.IsNullOrWhiteSpace(dto.ProjectManagerUserId))
            throw new ArgumentException("Project manager is required.");

        if (dto.ProjectManagerUserId != project.ProjectManagerUserId)
        {
            var pmAllowed = await _db.WorkspaceProjectManagers
                .AnyAsync(p => p.WorkspaceId == project.WorkspaceId
                            && p.UserId      == dto.ProjectManagerUserId, ct);
            if (!pmAllowed)
                throw new ArgumentException(
                    "The selected user is not on this workspace's Project Managers list. " +
                    "Add them on the workspace settings page first.");
        }

        // Block status changes from PendingApproval via normal update — only workflow review can do that
        if (project.Status == ProjectStatus.PendingApproval && dto.Status != ProjectStatus.PendingApproval)
            throw new ArgumentException("Project status can only be changed from PendingApproval through the approval workflow.");

        // Date freeze: block date changes when project is not in Draft
        if (project.Status != ProjectStatus.Draft)
        {
            if (dto.PlannedStartDate != project.PlannedStartDate || dto.PlannedEndDate != project.PlannedEndDate)
                throw new ArgumentException("Project dates cannot be changed after the project has been submitted for approval or activated.");
        }

        // ── Working-day rule (WO-2026-11 C-3) ─────────────────────────────────
        // Only planned dates whose day changes (Q8). The actual start date is
        // set to "now" on activation and is not checked.
        await _calendars.EnsureWorkingDaysAsync(project.WorkspaceId, new[]
        {
            WorkingDateCheck.Changed("Planned start date", dto.PlannedStartDate, project.PlannedStartDate),
            WorkingDateCheck.Changed("Planned end date",   dto.PlannedEndDate,   project.PlannedEndDate),
        }, ct);

        // ── Internal classification (WO-2026-11 P-1) ──────────────────────────
        // A label, not part of what was approved: changeable in every status this
        // update accepts, outside the date freeze. Null / missing keeps it.
        if (dto.ClearInternalClassification && dto.InternalClassificationId is { } both && both != Guid.Empty)
            throw new ArgumentException(
                "Send either an internal classification or clearInternalClassification, not both.");

        var internalClassificationId = project.InternalClassificationId;
        if (dto.ClearInternalClassification)
            internalClassificationId = null;
        else if (dto.InternalClassificationId is { } requestedClassification && requestedClassification != Guid.Empty)
        {
            await _classifications.EnsureAssignableAsync(
                project.WorkspaceId, requestedClassification, project.InternalClassificationId, ct);
            internalClassificationId = requestedClassification;
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
        project.InternalClassificationId = internalClassificationId;
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

        // Their sub-items go with them (WO-2026-11 A-2, D2).
        await ActionItemSubItemCascade.DeleteWithParentsAsync(_db, actionItems.Select(a => a.Id).ToList(), ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Project {ProjectCode} soft-deleted with {MilestoneCount} milestones and {ActionCount} action items",
            project.ProjectCode, milestones.Count, actionItems.Count);
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct)
    {
        var project = await _db.Projects
            .IgnoreQueryFilters()
            .WhereWorkspaceVisibleTo(_db.CurrentUserId)
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

        // Only the sub-items deleted together with these items (WO-2026-11 A-2, D2).
        await ActionItemSubItemCascade.RestoreWithParentsAsync(_db, actionItems.Select(a => a.Id).ToList(), ct);

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

        // Workspace must be explicitly linked to a strategic plan for its
        // projects to surface strategic objectives. Workspaces with no plan
        // return an empty list.
        if (!workspace.StrategicPlanId.HasValue)
            return new List<StrategicObjectiveOptionDto>();

        return await _db.StrategicObjectives
            .Where(so => so.StrategicPlanId == workspace.StrategicPlanId.Value)
            .OrderBy(so => so.ObjectiveCode)
            .Select(so => new StrategicObjectiveOptionDto
            {
                Id            = so.Id,
                ObjectiveCode = so.ObjectiveCode,
                Statement     = so.Statement,
            })
            .ToListAsync(ct);
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

    /// <summary>
    /// The project's internal classification when it may be shown: loaded
    /// through its query filter (live, not a private workspace the caller is
    /// outside of) and of the project's own workspace.
    /// </summary>
    private static WorkspaceProjectClassification? ShownClassification(Project p) =>
        p.InternalClassification is { IsDeleted: false } c && c.WorkspaceId == p.WorkspaceId ? c : null;

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
            InternalClassificationId    = ShownClassification(p)?.Id,
            InternalClassificationName  = ShownClassification(p)?.Name,
            InternalClassificationIsActive = ShownClassification(p)?.IsActive,
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
