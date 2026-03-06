using ActionTracker.Application.Features.Workspaces.DTOs;

namespace ActionTracker.Application.Features.Workspaces.Interfaces;

/// <summary>
/// Application service contract for workspace CRUD operations.
/// </summary>
public interface IWorkspaceService
{
    /// <summary>
    /// Returns a lightweight list of all workspaces regardless of active status.
    /// </summary>
    Task<IEnumerable<WorkspaceListDto>> GetAllWorkspacesAsync();

    /// <summary>
    /// Returns the full details of a single workspace, or <c>null</c> if not found.
    /// </summary>
    /// <param name="id">Primary key of the workspace.</param>
    Task<WorkspaceResponseDto?> GetWorkspaceByIdAsync(int id);

    /// <summary>
    /// Returns all workspaces where the given user is the designated admin.
    /// </summary>
    /// <param name="adminUserId">The AspNetUsers.Id of the admin user.</param>
    Task<IEnumerable<WorkspaceListDto>> GetWorkspacesByAdminUserIdAsync(string adminUserId);

    /// <summary>
    /// Creates a new workspace and returns its full representation.
    /// </summary>
    /// <param name="dto">Creation payload.</param>
    Task<WorkspaceResponseDto> CreateWorkspaceAsync(CreateWorkspaceDto dto);

    /// <summary>
    /// Updates an existing workspace and returns the updated representation,
    /// or <c>null</c> if the workspace does not exist.
    /// </summary>
    /// <param name="id">Primary key of the workspace to update.</param>
    /// <param name="dto">Update payload.</param>
    Task<WorkspaceResponseDto?> UpdateWorkspaceAsync(int id, UpdateWorkspaceDto dto);

    /// <summary>
    /// Soft-deletes a workspace (sets IsActive = false).
    /// Returns <c>true</c> if deleted, <c>false</c> if not found.
    /// </summary>
    /// <param name="id">Primary key of the workspace to delete.</param>
    Task<bool> DeleteWorkspaceAsync(int id);

    /// <summary>
    /// Restores a soft-deleted workspace (sets IsActive = true).
    /// Returns <c>true</c> if restored, <c>false</c> if not found.
    /// </summary>
    /// <param name="id">Primary key of the workspace to restore.</param>
    Task<bool> RestoreWorkspaceAsync(int id);

    /// <summary>
    /// Returns <c>true</c> if a workspace with the given primary key exists.
    /// </summary>
    /// <param name="id">Primary key to check.</param>
    Task<bool> WorkspaceExistsAsync(int id);

    /// <summary>
    /// Returns a flat list of non-deleted org units for use in dropdown menus,
    /// ordered alphabetically by name.
    /// </summary>
    Task<IEnumerable<OrgUnitDropdownItemDto>> GetOrgUnitsForDropdownAsync();

    /// <summary>
    /// Returns a list of active (IsActive = true) users for use in the workspace
    /// admin dropdown, ordered alphabetically by display name.
    /// </summary>
    Task<IEnumerable<UserDropdownItemDto>> GetActiveUsersForDropdownAsync();
}
