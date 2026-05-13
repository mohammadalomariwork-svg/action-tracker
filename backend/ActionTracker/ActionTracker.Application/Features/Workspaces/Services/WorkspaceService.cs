using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.Notifications.DTOs;
using ActionTracker.Application.Features.Workspaces.DTOs;
using ActionTracker.Application.Features.Workspaces.Interfaces;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActionTracker.Application.Features.Workspaces.Services;

/// <summary>
/// Implements <see cref="IWorkspaceService"/> using EF Core via
/// <see cref="IAppDbContext"/>. All mapping between the <see cref="Workspace"/>
/// entity and DTOs is done manually.
/// </summary>
public class WorkspaceService : IWorkspaceService
{
    private readonly IAppDbContext _db;
    private readonly ILogger<WorkspaceService> _logger;
    private readonly IEmailSender _emailSender;
    private readonly INotificationService _notificationService;
    private readonly AppSettings _appSettings;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Initialises a new instance of <see cref="WorkspaceService"/>.
    /// </summary>
    public WorkspaceService(
        IAppDbContext db,
        ILogger<WorkspaceService> logger,
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

    // -------------------------------------------------------------------------
    // Queries
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns all workspaces (active and inactive) ordered alphabetically by title.
    /// </summary>
    public async Task<IEnumerable<WorkspaceListDto>> GetAllWorkspacesAsync(List<Guid>? visibleWorkspaceIds = null)
    {
        try
        {
            var baseQuery = _db.Workspaces
                .Include(w => w.Admins)
                .AsQueryable();

            // Workspace-membership scoping: regular users only see workspaces
            // where they're a member or admin. A null list means no restriction.
            if (visibleWorkspaceIds != null)
            {
                if (visibleWorkspaceIds.Count == 0)
                    baseQuery = baseQuery.Where(_ => false);
                else
                    baseQuery = baseQuery.Where(w => visibleWorkspaceIds.Contains(w.Id));
            }

            var list = await baseQuery
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new WorkspaceListDto
                {
                    Id               = w.Id,
                    Title            = w.Title,
                    OrganizationUnit = w.OrganizationUnit,
                    OrgUnitId        = w.OrgUnitId,
                    AdminUserNames   = string.Join(", ", w.Admins.Select(a => a.AdminUserName)),
                    IsActive         = w.IsActive,
                    CreatedAt        = w.CreatedAt,
                    ProjectCount     = _db.Projects.Count(p => p.WorkspaceId == w.Id && !p.IsDeleted),
                    MilestoneCount   = _db.Milestones.Count(m => _db.Projects.Any(p => p.Id == m.ProjectId && p.WorkspaceId == w.Id && !p.IsDeleted) && !m.IsDeleted),
                    ActionItemCount  = _db.ActionItems.Count(a => a.WorkspaceId == w.Id && !a.IsDeleted),
                    OpenActionItemCount = _db.ActionItems.Count(a => a.WorkspaceId == w.Id && !a.IsDeleted && a.Status != ActionStatus.Done)
                })
                .ToListAsync();

            await EnrichListAdminsAsync(list);
            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all workspaces");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<WorkspaceSummaryDto> GetSummaryAsync(List<Guid>? visibleWorkspaceIds = null)
    {
        try
        {
            // Short-circuit: caller explicitly passed an empty scope list → no access.
            if (visibleWorkspaceIds != null && visibleWorkspaceIds.Count == 0)
                return new WorkspaceSummaryDto();

            // Build the scoped workspace query (mirrors GetAllWorkspacesAsync logic).
            IQueryable<Workspace> workspaceScope = _db.Workspaces;
            if (visibleWorkspaceIds != null)
                workspaceScope = workspaceScope.Where(w => visibleWorkspaceIds.Contains(w.Id));

            // All project / action-item counts are filtered through the scoped workspace IDs.
            var scopedWorkspaceIds = workspaceScope.Select(w => w.Id);

            var totalWorkspaces  = await workspaceScope.CountAsync();
            var activeWorkspaces = await workspaceScope.CountAsync(w => w.IsActive);

            var strategicProjects = await _db.Projects
                .CountAsync(p => !p.IsDeleted
                    && p.ProjectType == ProjectType.Strategic
                    && scopedWorkspaceIds.Contains(p.WorkspaceId));
            var operationalProjects = await _db.Projects
                .CountAsync(p => !p.IsDeleted
                    && p.ProjectType == ProjectType.Operational
                    && scopedWorkspaceIds.Contains(p.WorkspaceId));

            var standaloneActionItems = await _db.ActionItems
                .CountAsync(a => !a.IsDeleted && a.IsStandalone
                    && scopedWorkspaceIds.Contains(a.WorkspaceId));
            var projectActionItems = await _db.ActionItems
                .CountAsync(a => !a.IsDeleted && !a.IsStandalone
                    && scopedWorkspaceIds.Contains(a.WorkspaceId));
            var strategicActionItems = await _db.ActionItems
                .CountAsync(a => !a.IsDeleted && !a.IsStandalone
                    && scopedWorkspaceIds.Contains(a.WorkspaceId)
                    && a.Project != null && a.Project.ProjectType == ProjectType.Strategic);

            // Project completion & on-time delivery rates
            var totalProjects = strategicProjects + operationalProjects;
            var completedProjects = await _db.Projects
                .CountAsync(p => !p.IsDeleted
                    && p.Status == ProjectStatus.Completed
                    && scopedWorkspaceIds.Contains(p.WorkspaceId));
            var completedProjectsOnTime = await _db.Projects
                .CountAsync(p => !p.IsDeleted
                    && p.Status == ProjectStatus.Completed
                    && p.UpdatedAt != null && p.UpdatedAt <= p.PlannedEndDate
                    && scopedWorkspaceIds.Contains(p.WorkspaceId));

            decimal projectCompletionRate = totalProjects > 0
                ? Math.Round((decimal)completedProjects / totalProjects * 100, 1) : 0;
            decimal projectOnTimeRate = completedProjects > 0
                ? Math.Round((decimal)completedProjectsOnTime / completedProjects * 100, 1) : 0;

            // Standalone action items completion & on-time delivery rates
            var standaloneDone = await _db.ActionItems
                .CountAsync(a => !a.IsDeleted && a.IsStandalone
                    && scopedWorkspaceIds.Contains(a.WorkspaceId)
                    && a.Status == ActionStatus.Done);
            var standaloneDoneOnTime = await _db.ActionItems
                .CountAsync(a => !a.IsDeleted && a.IsStandalone
                    && scopedWorkspaceIds.Contains(a.WorkspaceId)
                    && a.Status == ActionStatus.Done && a.UpdatedAt <= a.DueDate);

            decimal standaloneCompletionRate = standaloneActionItems > 0
                ? Math.Round((decimal)standaloneDone / standaloneActionItems * 100, 1) : 0;
            decimal standaloneOnTimeRate = standaloneDone > 0
                ? Math.Round((decimal)standaloneDoneOnTime / standaloneDone * 100, 1) : 0;

            return new WorkspaceSummaryDto
            {
                TotalWorkspaces              = totalWorkspaces,
                ActiveWorkspaces             = activeWorkspaces,
                StrategicProjects            = strategicProjects,
                OperationalProjects          = operationalProjects,
                StandaloneActionItems        = standaloneActionItems,
                ProjectActionItems           = projectActionItems,
                StrategicActionItems         = strategicActionItems,
                ProjectCompletionRate        = projectCompletionRate,
                ProjectOnTimeDeliveryRate    = projectOnTimeRate,
                StandaloneCompletionRate     = standaloneCompletionRate,
                StandaloneOnTimeDeliveryRate = standaloneOnTimeRate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing workspace summary");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<WorkspaceStatsDto?> GetWorkspaceStatsAsync(Guid workspaceId)
    {
        try
        {
            var exists = await _db.Workspaces.AnyAsync(w => w.Id == workspaceId);
            if (!exists) return null;

            var totalProjects = await _db.Projects
                .CountAsync(p => p.WorkspaceId == workspaceId && !p.IsDeleted);
            var strategicProjects = await _db.Projects
                .CountAsync(p => p.WorkspaceId == workspaceId && !p.IsDeleted && p.ProjectType == ProjectType.Strategic);
            var baselinedProjects = await _db.Projects
                .CountAsync(p => p.WorkspaceId == workspaceId && !p.IsDeleted && p.IsBaselined);
            var nonBaselinedProjects = await _db.Projects
                .CountAsync(p => p.WorkspaceId == workspaceId && !p.IsDeleted && !p.IsBaselined);

            // Project completion & on-time rates
            var completedProjects = await _db.Projects
                .CountAsync(p => p.WorkspaceId == workspaceId && !p.IsDeleted && p.Status == ProjectStatus.Completed);
            var completedProjectsOnTime = await _db.Projects
                .CountAsync(p => p.WorkspaceId == workspaceId && !p.IsDeleted
                    && p.Status == ProjectStatus.Completed
                    && p.UpdatedAt != null && p.UpdatedAt <= p.PlannedEndDate);

            decimal projectCompletionRate = totalProjects > 0
                ? Math.Round((decimal)completedProjects / totalProjects * 100, 1) : 0;
            decimal projectOnTimeRate = completedProjects > 0
                ? Math.Round((decimal)completedProjectsOnTime / completedProjects * 100, 1) : 0;

            var totalActionItems = await _db.ActionItems
                .CountAsync(a => a.WorkspaceId == workspaceId && !a.IsDeleted);
            var standaloneActionItems = await _db.ActionItems
                .CountAsync(a => a.WorkspaceId == workspaceId && !a.IsDeleted && a.IsStandalone);
            var escalatedActionItems = await _db.ActionItems
                .CountAsync(a => a.WorkspaceId == workspaceId && !a.IsDeleted && a.IsEscalated);

            // Standalone action items completion & on-time rates
            var standaloneDone = await _db.ActionItems
                .CountAsync(a => a.WorkspaceId == workspaceId && !a.IsDeleted && a.IsStandalone
                    && a.Status == ActionStatus.Done);
            var standaloneDoneOnTime = await _db.ActionItems
                .CountAsync(a => a.WorkspaceId == workspaceId && !a.IsDeleted && a.IsStandalone
                    && a.Status == ActionStatus.Done && a.UpdatedAt <= a.DueDate);

            decimal standaloneCompletionRate = standaloneActionItems > 0
                ? Math.Round((decimal)standaloneDone / standaloneActionItems * 100, 1) : 0;
            decimal standaloneOnTimeRate = standaloneDone > 0
                ? Math.Round((decimal)standaloneDoneOnTime / standaloneDone * 100, 1) : 0;

            return new WorkspaceStatsDto
            {
                TotalProjects                = totalProjects,
                StrategicProjects            = strategicProjects,
                BaselinedProjects            = baselinedProjects,
                NonBaselinedProjects         = nonBaselinedProjects,
                ProjectCompletionRate        = projectCompletionRate,
                ProjectOnTimeDeliveryRate    = projectOnTimeRate,
                TotalActionItems             = totalActionItems,
                StandaloneActionItems        = standaloneActionItems,
                EscalatedActionItems         = escalatedActionItems,
                StandaloneCompletionRate     = standaloneCompletionRate,
                StandaloneOnTimeDeliveryRate = standaloneOnTimeRate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing stats for workspace {WorkspaceId}", workspaceId);
            throw;
        }
    }

    /// <summary>
    /// Returns the full details of a single workspace, or <c>null</c> if not found.
    /// </summary>
    /// <param name="id">Primary key of the workspace.</param>
    public async Task<WorkspaceResponseDto?> GetWorkspaceByIdAsync(Guid id)
    {
        try
        {
            var workspace = await _db.Workspaces
                .Include(w => w.Admins)
                .Include(w => w.Members)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workspace is null) return null;

            var dto = ToResponseDto(workspace);
            await EnrichAdminDtosAsync(dto.Admins);
            await EnrichMemberDtosAsync(dto.Members);

            if (workspace.StrategicPlanId.HasValue)
            {
                dto.StrategicPlanName = await _db.StrategicPlans
                    .Where(p => p.Id == workspace.StrategicPlanId.Value)
                    .Select(p => p.Name)
                    .FirstOrDefaultAsync();
            }

            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workspace {WorkspaceId}", id);
            throw;
        }
    }

    /// <summary>
    /// Returns all active workspaces where the given user is one of the admins.
    /// </summary>
    /// <param name="adminUserId">The AspNetUsers.Id of the admin user.</param>
    public async Task<IEnumerable<WorkspaceListDto>> GetWorkspacesByAdminUserIdAsync(string adminUserId)
    {
        try
        {
            var list = await _db.Workspaces
                .Include(w => w.Admins)
                .Where(w => w.IsActive && w.Admins.Any(a => a.AdminUserId == adminUserId))
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new WorkspaceListDto
                {
                    Id               = w.Id,
                    Title            = w.Title,
                    OrganizationUnit = w.OrganizationUnit,
                    AdminUserNames   = string.Join(", ", w.Admins.Select(a => a.AdminUserName)),
                    IsActive         = w.IsActive,
                    CreatedAt        = w.CreatedAt,
                    ProjectCount     = _db.Projects.Count(p => p.WorkspaceId == w.Id && !p.IsDeleted),
                    MilestoneCount   = _db.Milestones.Count(m => _db.Projects.Any(p => p.Id == m.ProjectId && p.WorkspaceId == w.Id && !p.IsDeleted) && !m.IsDeleted),
                    ActionItemCount  = _db.ActionItems.Count(a => a.WorkspaceId == w.Id && !a.IsDeleted),
                    OpenActionItemCount = _db.ActionItems.Count(a => a.WorkspaceId == w.Id && !a.IsDeleted && a.Status != ActionStatus.Done)
                })
                .ToListAsync();

            await EnrichListAdminsAsync(list);
            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workspaces for admin user {AdminUserId}", adminUserId);
            throw;
        }
    }

    /// <summary>
    /// Returns <c>true</c> if an active workspace with the given id exists.
    /// </summary>
    /// <param name="id">Primary key to check.</param>
    public async Task<bool> WorkspaceExistsAsync(Guid id)
    {
        try
        {
            return await _db.Workspaces
                .AnyAsync(w => w.Id == id && w.IsActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence of workspace {WorkspaceId}", id);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // Commands
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a new workspace and returns its full representation.
    /// </summary>
    /// <param name="dto">Creation payload.</param>
    public async Task<WorkspaceResponseDto> CreateWorkspaceAsync(CreateWorkspaceDto dto)
    {
        try
        {
            var workspace = new Workspace
            {
                Id               = Guid.NewGuid(),
                Title            = dto.Title,
                OrganizationUnit = dto.OrganizationUnit,
                OrgUnitId        = dto.OrgUnitId,
                StrategicPlanId  = dto.StrategicPlanId,
                CreatedAt        = DateTime.UtcNow,
                IsActive         = true
            };

            foreach (var a in dto.Admins)
            {
                workspace.Admins.Add(new WorkspaceAdmin
                {
                    AdminUserId   = a.UserId,
                    AdminUserName = a.UserName
                });
            }

            _db.Workspaces.Add(workspace);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Workspace {WorkspaceId} created with {AdminCount} admin(s)",
                workspace.Id, workspace.Admins.Count);

            // Fire-and-forget email notification
            var capturedWorkspace = workspace;
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

                try
                {
                    var placeholders = new Dictionary<string, string>
                    {
                        ["WorkspaceName"] = capturedWorkspace.Title,
                        ["OrgUnit"]       = capturedWorkspace.OrganizationUnit,
                        ["CreatedBy"]     = string.Empty,
                        ["ItemUrl"]       = $"{_appSettings.FrontendBaseUrl}/workspaces/{capturedWorkspace.Id}",
                    };

                    var adminUserIds = capturedWorkspace.Admins.Select(a => a.AdminUserId).ToList();
                    var adminEmails = await db.Users
                        .Where(u => adminUserIds.Contains(u.Id) && u.IsActive && u.Email != null)
                        .Select(u => u.Email!)
                        .Distinct()
                        .ToListAsync();

                    if (adminEmails.Count > 0)
                    {
                        await emailSender.SendEmailAsync("Workspace.Created", placeholders,
                            adminEmails, "Workspace", capturedWorkspace.Id);
                    }
                }
                catch (Exception ex2)
                {
                    _logger.LogError(ex2, "Error sending email for Workspace.Created {Id}", capturedWorkspace.Id);
                }

                // In-app notifications
                try
                {
                    var url = $"{_appSettings.FrontendBaseUrl}/workspaces/{capturedWorkspace.Id}";
                    var notifications = capturedWorkspace.Admins
                        .Select(a => new CreateNotificationDto
                        {
                            UserId               = a.AdminUserId,
                            Title                = "New Workspace",
                            Message              = $"Workspace {capturedWorkspace.Title} has been created",
                            Type                 = "Workspace",
                            ActionType           = "Created",
                            RelatedEntityType    = "Workspace",
                            RelatedEntityId      = capturedWorkspace.Id,
                            Url                  = url,
                        }).ToList();
                    if (notifications.Count > 0)
                        await notifService.CreateBulkAsync(notifications);
                }
                catch (Exception ex3)
                {
                    _logger.LogError(ex3, "Error creating notifications for Workspace.Created {Id}", capturedWorkspace.Id);
                }
            });

            var responseDto = ToResponseDto(workspace);
            await EnrichAdminDtosAsync(responseDto.Admins);
            return responseDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating workspace");
            throw;
        }
    }

    /// <summary>
    /// Updates an existing workspace. Returns the updated representation, or
    /// <c>null</c> if the workspace does not exist.
    /// </summary>
    /// <param name="id">Primary key of the workspace to update.</param>
    /// <param name="dto">Update payload.</param>
    public async Task<WorkspaceResponseDto?> UpdateWorkspaceAsync(Guid id, UpdateWorkspaceDto dto)
    {
        try
        {
            var workspace = await _db.Workspaces
                .Include(w => w.Admins)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workspace is null)
            {
                _logger.LogWarning("UpdateWorkspaceAsync: workspace {WorkspaceId} not found", id);
                return null;
            }

            workspace.Title            = dto.Title;
            workspace.OrganizationUnit = dto.OrganizationUnit;
            workspace.OrgUnitId        = dto.OrgUnitId;
            workspace.StrategicPlanId  = dto.StrategicPlanId;
            workspace.IsActive         = dto.IsActive;
            workspace.UpdatedAt        = DateTime.UtcNow;

            // Replace admin list
            _db.WorkspaceAdmins.RemoveRange(workspace.Admins);
            workspace.Admins.Clear();

            foreach (var a in dto.Admins)
            {
                workspace.Admins.Add(new WorkspaceAdmin
                {
                    WorkspaceId   = id,
                    AdminUserId   = a.UserId,
                    AdminUserName = a.UserName
                });
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation("Workspace {WorkspaceId} updated with {AdminCount} admin(s)",
                id, workspace.Admins.Count);

            var responseDto = ToResponseDto(workspace);
            await EnrichAdminDtosAsync(responseDto.Admins);
            return responseDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating workspace {WorkspaceId}", id);
            throw;
        }
    }

    /// <summary>
    /// Soft-deletes a workspace by setting <c>IsActive = false</c>.
    /// Returns <c>false</c> if the workspace does not exist.
    /// </summary>
    /// <param name="id">Primary key of the workspace to delete.</param>
    public async Task<bool> DeleteWorkspaceAsync(Guid id)
    {
        try
        {
            var workspace = await _db.Workspaces
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workspace is null)
            {
                _logger.LogWarning("DeleteWorkspaceAsync: workspace {WorkspaceId} not found", id);
                return false;
            }

            workspace.IsActive  = false;
            workspace.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            _logger.LogInformation("Workspace {WorkspaceId} soft-deleted", id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting workspace {WorkspaceId}", id);
            throw;
        }
    }

    /// <summary>
    /// Restores a soft-deleted workspace by setting <c>IsActive = true</c>.
    /// Returns <c>false</c> if the workspace does not exist.
    /// </summary>
    /// <param name="id">Primary key of the workspace to restore.</param>
    public async Task<bool> RestoreWorkspaceAsync(Guid id)
    {
        try
        {
            var workspace = await _db.Workspaces
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workspace is null)
            {
                _logger.LogWarning("RestoreWorkspaceAsync: workspace {WorkspaceId} not found", id);
                return false;
            }

            workspace.IsActive  = true;
            workspace.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            _logger.LogInformation("Workspace {WorkspaceId} restored", id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring workspace {WorkspaceId}", id);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // Dropdown helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns all non-deleted org units in tree-traversal order (parent before
    /// children, siblings sorted alphabetically) with Level and Code so the
    /// frontend can render a visually indented hierarchy.
    /// </summary>
    public async Task<IEnumerable<OrgUnitDropdownItemDto>> GetOrgUnitsForDropdownAsync()
    {
        try
        {
            var all = await _db.OrgUnits
                .Where(o => !o.IsDeleted)
                .Select(o => new { o.Id, o.Name, o.Code, o.Level, o.ParentId })
                .ToListAsync();

            var lookup = all.ToLookup(o => o.ParentId);
            var result = new List<OrgUnitDropdownItemDto>(all.Count);

            void Flatten(Guid? parentId)
            {
                foreach (var o in lookup[parentId].OrderBy(o => o.Name))
                {
                    result.Add(new OrgUnitDropdownItemDto
                    {
                        Id    = o.Id.ToString(),
                        Name  = o.Name,
                        Code  = o.Code,
                        Level = o.Level
                    });
                    Flatten(o.Id);
                }
            }

            Flatten(null);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving org units for dropdown");
            throw;
        }
    }

    /// <summary>
    /// Returns active users ordered by display name for use in the admin dropdown.
    /// </summary>
    public async Task<IEnumerable<UserDropdownItemDto>> GetActiveUsersForDropdownAsync()
    {
        try
        {
            return await _db.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.DisplayName ?? (u.FirstName + " " + u.LastName))
                .Select(u => new UserDropdownItemDto
                {
                    Id          = u.Id,
                    DisplayName = u.DisplayName ?? (u.FirstName + " " + u.LastName),
                    Email       = u.Email,
                    OrgUnitName = u.OrgUnitId == null
                        ? null
                        : _db.OrgUnits
                            .Where(o => o.Id == u.OrgUnitId)
                            .Select(o => o.Name)
                            .FirstOrDefault()
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active users for dropdown");
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Populates <see cref="WorkspaceAdminDto.Email"/> and
    /// <see cref="WorkspaceAdminDto.OrgUnitName"/> by joining with the Users
    /// and OrgUnits tables.
    /// </summary>
    private async Task EnrichAdminDtosAsync(List<WorkspaceAdminDto> admins)
    {
        if (admins.Count == 0) return;

        var userIds = admins.Select(a => a.UserId).ToList();

        _logger.LogDebug("EnrichAdminDtosAsync: looking up {Count} user(s): {UserIds}",
            userIds.Count, string.Join(", ", userIds));

        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.OrgUnitId })
            .ToListAsync();

        _logger.LogDebug("EnrichAdminDtosAsync: found {FoundCount} of {RequestedCount} user(s)",
            users.Count, userIds.Count);

        var orgUnitIds = users
            .Where(u => u.OrgUnitId.HasValue)
            .Select(u => u.OrgUnitId!.Value)
            .Distinct()
            .ToList();

        var orgUnits = orgUnitIds.Count > 0
            ? await _db.OrgUnits
                .Where(o => orgUnitIds.Contains(o.Id))
                .Select(o => new { o.Id, o.Name })
                .ToDictionaryAsync(o => o.Id, o => o.Name)
            : new Dictionary<Guid, string>();

        _logger.LogDebug("EnrichAdminDtosAsync: resolved {OrgUnitCount} org unit(s)", orgUnits.Count);

        foreach (var admin in admins)
        {
            var user = users.FirstOrDefault(u => u.Id == admin.UserId);
            if (user is null)
            {
                _logger.LogWarning("EnrichAdminDtosAsync: user {UserId} not found in AspNetUsers", admin.UserId);
                continue;
            }

            admin.Email = user.Email ?? string.Empty;
            admin.OrgUnitName = user.OrgUnitId.HasValue && orgUnits.TryGetValue(user.OrgUnitId.Value, out var name)
                ? name
                : string.Empty;
        }
    }

    /// <summary>
    /// Populates <see cref="WorkspaceListDto.Admins"/> with admin name + department
    /// by looking up workspace admins and joining with Users/OrgUnits.
    /// </summary>
    private async Task EnrichListAdminsAsync(List<WorkspaceListDto> list)
    {
        if (list.Count == 0) return;

        var workspaceIds = list.Select(w => w.Id).ToList();

        // Get all workspace admins for the workspaces in question
        var admins = await _db.WorkspaceAdmins
            .Where(wa => workspaceIds.Contains(wa.WorkspaceId))
            .Select(wa => new { wa.WorkspaceId, wa.AdminUserId, wa.AdminUserName })
            .ToListAsync();

        var userIds = admins.Select(a => a.AdminUserId).Distinct().ToList();

        // Get user org unit mapping
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.OrgUnitId })
            .ToListAsync();

        var orgUnitIds = users
            .Where(u => u.OrgUnitId.HasValue)
            .Select(u => u.OrgUnitId!.Value)
            .Distinct()
            .ToList();

        var orgUnits = orgUnitIds.Count > 0
            ? await _db.OrgUnits
                .Where(o => orgUnitIds.Contains(o.Id))
                .Select(o => new { o.Id, o.Name })
                .ToDictionaryAsync(o => o.Id, o => o.Name)
            : new Dictionary<Guid, string>();

        foreach (var dto in list)
        {
            dto.Admins = admins
                .Where(a => a.WorkspaceId == dto.Id)
                .Select(a =>
                {
                    var user = users.FirstOrDefault(u => u.Id == a.AdminUserId);
                    var dept = user?.OrgUnitId.HasValue == true && orgUnits.TryGetValue(user.OrgUnitId!.Value, out var name)
                        ? name
                        : string.Empty;
                    return new WorkspaceListAdminDto { Name = a.AdminUserName, Department = dept };
                })
                .ToList();
        }
    }

    // -------------------------------------------------------------------------
    // Private mapping helpers
    // -------------------------------------------------------------------------

    private static WorkspaceResponseDto ToResponseDto(Workspace w) => new()
    {
        Id               = w.Id,
        Title            = w.Title,
        OrganizationUnit = w.OrganizationUnit,
        OrgUnitId        = w.OrgUnitId,
        StrategicPlanId  = w.StrategicPlanId,
        Admins           = w.Admins
                            .Select(a => new WorkspaceAdminDto { UserId = a.AdminUserId, UserName = a.AdminUserName })
                            .ToList(),
        Members          = w.Members
                            .Select(m => new WorkspaceMemberDto
                            {
                                UserId   = m.UserId,
                                UserName = m.UserName,
                                AddedAt  = m.AddedAt,
                            })
                            .ToList(),
        IsActive         = w.IsActive,
        CreatedAt        = w.CreatedAt,
        UpdatedAt        = w.UpdatedAt
    };

    private static WorkspaceListDto ToListDto(Workspace w) => new()
    {
        Id               = w.Id,
        Title            = w.Title,
        OrganizationUnit = w.OrganizationUnit,
        AdminUserNames   = string.Join(", ", w.Admins.Select(a => a.AdminUserName)),
        IsActive         = w.IsActive,
        CreatedAt        = w.CreatedAt
    };

    // -------------------------------------------------------------------------
    // Members CRUD
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<IEnumerable<WorkspaceMemberDto>> GetMembersAsync(Guid workspaceId)
    {
        var members = await _db.WorkspaceMembers
            .Where(m => m.WorkspaceId == workspaceId)
            .OrderBy(m => m.UserName)
            .Select(m => new WorkspaceMemberDto
            {
                UserId   = m.UserId,
                UserName = m.UserName,
                AddedAt  = m.AddedAt,
            })
            .ToListAsync();

        await EnrichMemberDtosAsync(members);
        return members;
    }

    /// <inheritdoc />
    public async Task<WorkspaceMemberDto?> AddMemberAsync(Guid workspaceId, AddWorkspaceMemberDto dto, string? addedByUserId)
    {
        var workspaceExists = await _db.Workspaces.AnyAsync(w => w.Id == workspaceId);
        if (!workspaceExists) return null;

        var alreadyExists = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == workspaceId && m.UserId == dto.UserId);
        if (alreadyExists) return null;

        var member = new WorkspaceMember
        {
            WorkspaceId   = workspaceId,
            UserId        = dto.UserId,
            UserName      = dto.UserName,
            AddedAt       = DateTime.UtcNow,
            AddedByUserId = addedByUserId,
        };
        _db.WorkspaceMembers.Add(member);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Added user {UserId} to workspace {WorkspaceId}", dto.UserId, workspaceId);

        var result = new WorkspaceMemberDto
        {
            UserId   = member.UserId,
            UserName = member.UserName,
            AddedAt  = member.AddedAt,
        };
        await EnrichMemberDtosAsync(new List<WorkspaceMemberDto> { result });
        return result;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveMemberAsync(Guid workspaceId, string userId)
    {
        var member = await _db.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId);
        if (member is null) return false;

        _db.WorkspaceMembers.Remove(member);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Removed user {UserId} from workspace {WorkspaceId}", userId, workspaceId);
        return true;
    }

    /// <inheritdoc />
    public async Task<WorkspaceResponseDto?> SetStrategyAsync(Guid workspaceId, Guid? strategicPlanId)
    {
        var workspace = await _db.Workspaces
            .Include(w => w.Admins)
            .Include(w => w.Members)
            .FirstOrDefaultAsync(w => w.Id == workspaceId);
        if (workspace is null) return null;

        if (strategicPlanId.HasValue)
        {
            var planExists = await _db.StrategicPlans.AnyAsync(p => p.Id == strategicPlanId.Value);
            if (!planExists)
                throw new KeyNotFoundException($"Strategic plan '{strategicPlanId}' not found.");
        }

        workspace.StrategicPlanId = strategicPlanId;
        workspace.UpdatedAt       = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Workspace {WorkspaceId} strategic plan set to {StrategicPlanId}",
            workspaceId, strategicPlanId);

        var dto = ToResponseDto(workspace);
        await EnrichAdminDtosAsync(dto.Admins);
        await EnrichMemberDtosAsync(dto.Members);
        if (workspace.StrategicPlanId.HasValue)
        {
            dto.StrategicPlanName = await _db.StrategicPlans
                .Where(p => p.Id == workspace.StrategicPlanId.Value)
                .Select(p => p.Name)
                .FirstOrDefaultAsync();
        }
        return dto;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<StrategicPlanDropdownItemDto>> GetStrategicPlansForDropdownAsync()
    {
        return await _db.StrategicPlans
            .OrderByDescending(p => p.StartYear)
            .ThenBy(p => p.Name)
            .Select(p => new StrategicPlanDropdownItemDto
            {
                Id          = p.Id,
                Name        = p.Name,
                OrgUnitId   = p.OrgUnitId,
                OrgUnitName = p.OrgUnit != null ? p.OrgUnit.Name : string.Empty,
                StartYear   = p.StartYear,
                EndYear     = p.StartYear + p.DurationYears - 1,
                Status      = p.Status.ToString(),
            })
            .ToListAsync();
    }

    private async Task EnrichMemberDtosAsync(List<WorkspaceMemberDto> members)
    {
        if (members.Count == 0) return;

        var userIds = members.Select(m => m.UserId).ToList();
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.OrgUnitId })
            .ToListAsync();

        var orgUnitIds = users
            .Where(u => u.OrgUnitId.HasValue)
            .Select(u => u.OrgUnitId!.Value)
            .Distinct()
            .ToList();

        var orgUnits = orgUnitIds.Count > 0
            ? await _db.OrgUnits
                .Where(o => orgUnitIds.Contains(o.Id))
                .Select(o => new { o.Id, o.Name })
                .ToDictionaryAsync(o => o.Id, o => o.Name)
            : new Dictionary<Guid, string>();

        foreach (var m in members)
        {
            var u = users.FirstOrDefault(x => x.Id == m.UserId);
            if (u is null) continue;
            m.Email       = u.Email ?? string.Empty;
            m.OrgUnitName = u.OrgUnitId.HasValue && orgUnits.TryGetValue(u.OrgUnitId.Value, out var name)
                ? name : string.Empty;
        }
    }
}
