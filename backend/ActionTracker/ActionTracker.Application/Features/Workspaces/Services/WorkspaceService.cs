using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Workspaces.DTOs;
using ActionTracker.Application.Features.Workspaces.Interfaces;
using ActionTracker.Application.Features.Workspaces.Models;
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
    /// Returns all active workspaces ordered alphabetically by title.
    /// </summary>
    public async Task<IEnumerable<WorkspaceListDto>> GetAllWorkspacesAsync()
    {
        try
        {
            return await _db.Workspaces
                .Where(w => w.IsActive)
                .OrderBy(w => w.Title)
                .Select(w => ToListDto(w))
                .ToListAsync();
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
    public async Task<WorkspaceResponseDto?> GetWorkspaceByIdAsync(int id)
    {
        try
        {
            var workspace = await _db.Workspaces
                .FirstOrDefaultAsync(w => w.Id == id);

            return workspace is null ? null : ToResponseDto(workspace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workspace {WorkspaceId}", id);
            throw;
        }
    }

    /// <summary>
    /// Returns all active workspaces where the given user is the admin.
    /// </summary>
    /// <param name="adminUserId">The AspNetUsers.Id of the admin user.</param>
    public async Task<IEnumerable<WorkspaceListDto>> GetWorkspacesByAdminUserIdAsync(string adminUserId)
    {
        try
        {
            return await _db.Workspaces
                .Where(w => w.AdminUserId == adminUserId && w.IsActive)
                .OrderBy(w => w.Title)
                .Select(w => ToListDto(w))
                .ToListAsync();
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
    public async Task<bool> WorkspaceExistsAsync(int id)
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
                Title            = dto.Title,
                OrganizationUnit = dto.OrganizationUnit,
                AdminUserId      = dto.AdminUserId,
                AdminUserName    = dto.AdminUserName,
                CreatedAt        = DateTime.UtcNow,
                IsActive         = true
            };

            _db.Workspaces.Add(workspace);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Workspace {WorkspaceId} created", workspace.Id);

            return ToResponseDto(workspace);
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
    public async Task<WorkspaceResponseDto?> UpdateWorkspaceAsync(int id, UpdateWorkspaceDto dto)
    {
        try
        {
            var workspace = await _db.Workspaces
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workspace is null)
            {
                _logger.LogWarning("UpdateWorkspaceAsync: workspace {WorkspaceId} not found", id);
                return null;
            }

            workspace.Title            = dto.Title;
            workspace.OrganizationUnit = dto.OrganizationUnit;
            workspace.AdminUserId      = dto.AdminUserId;
            workspace.AdminUserName    = dto.AdminUserName;
            workspace.IsActive         = dto.IsActive;
            workspace.UpdatedAt        = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            _logger.LogInformation("Workspace {WorkspaceId} updated", id);

            return ToResponseDto(workspace);
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
    public async Task<bool> DeleteWorkspaceAsync(int id)
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

    // -------------------------------------------------------------------------
    // Private mapping helpers
    // -------------------------------------------------------------------------

    private static WorkspaceResponseDto ToResponseDto(Workspace w) => new()
    {
        Id               = w.Id,
        Title            = w.Title,
        OrganizationUnit = w.OrganizationUnit,
        AdminUserId      = w.AdminUserId,
        AdminUserName    = w.AdminUserName,
        IsActive         = w.IsActive,
        CreatedAt        = w.CreatedAt,
        UpdatedAt        = w.UpdatedAt
    };

    private static WorkspaceListDto ToListDto(Workspace w) => new()
    {
        Id               = w.Id,
        Title            = w.Title,
        OrganizationUnit = w.OrganizationUnit,
        AdminUserName    = w.AdminUserName,
        IsActive         = w.IsActive
    };
}
