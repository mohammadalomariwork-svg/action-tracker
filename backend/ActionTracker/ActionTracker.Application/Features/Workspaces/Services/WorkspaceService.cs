using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Workspaces.DTOs;
using ActionTracker.Application.Features.Workspaces.Interfaces;
using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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

    /// <summary>
    /// Initialises a new instance of <see cref="WorkspaceService"/>.
    /// </summary>
    public WorkspaceService(IAppDbContext db, ILogger<WorkspaceService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // Queries
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns all workspaces (active and inactive) ordered alphabetically by title.
    /// </summary>
    public async Task<IEnumerable<WorkspaceListDto>> GetAllWorkspacesAsync()
    {
        try
        {
            var list = await _db.Workspaces
                .Include(w => w.Admins)
                .OrderBy(w => w.Title)
                .ToListAsync();

            return list.Select(ToListDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all workspaces");
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
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workspace is null) return null;

            var dto = ToResponseDto(workspace);
            await EnrichAdminDtosAsync(dto.Admins);
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
                .OrderBy(w => w.Title)
                .ToListAsync();

            return list.Select(ToListDto);
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
                    DisplayName = u.DisplayName ?? (u.FirstName + " " + u.LastName)
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

    // -------------------------------------------------------------------------
    // Private mapping helpers
    // -------------------------------------------------------------------------

    private static WorkspaceResponseDto ToResponseDto(Workspace w) => new()
    {
        Id               = w.Id,
        Title            = w.Title,
        OrganizationUnit = w.OrganizationUnit,
        Admins           = w.Admins
                            .Select(a => new WorkspaceAdminDto { UserId = a.AdminUserId, UserName = a.AdminUserName })
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
        IsActive         = w.IsActive
    };
}
