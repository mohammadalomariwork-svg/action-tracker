using System;
using ActionTracker.Application.Features.Workspaces.DTOs;

namespace ActionTracker.Application.Features.Workspaces.Interfaces;

/// <summary>
/// Application service contract for workspace CRUD operations.
/// </summary>
public interface IWorkspaceService
{
    /// <summary>
    /// Returns a lightweight list of workspaces. When <paramref name="visibleWorkspaceIds"/>
    /// is non-null the result is restricted to those IDs (an empty list yields no rows).
    /// A <c>null</c> value means "no restriction".
    /// </summary>
    Task<IEnumerable<WorkspaceListDto>> GetAllWorkspacesAsync(List<Guid>? visibleWorkspaceIds = null);

    /// <summary>
    /// Returns aggregate statistics scoped to the given workspace IDs. A <c>null</c>
    /// value means "no restriction"; an empty list yields zeros.
    /// </summary>
    Task<WorkspaceSummaryDto> GetSummaryAsync(List<Guid>? visibleWorkspaceIds = null);

    /// <summary>Returns per-workspace statistics for the workspace detail dashboard.</summary>
    /// <param name="workspaceId">Primary key of the workspace.</param>
    Task<WorkspaceStatsDto?> GetWorkspaceStatsAsync(Guid workspaceId);

    /// <summary>
    /// Returns the full details of a single workspace, or <c>null</c> if not found.
    /// </summary>
    /// <param name="id">Primary key of the workspace.</param>
    Task<WorkspaceResponseDto?> GetWorkspaceByIdAsync(Guid id);

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
    Task<WorkspaceResponseDto?> UpdateWorkspaceAsync(Guid id, UpdateWorkspaceDto dto);

    /// <summary>
    /// Soft-deletes a workspace (sets IsActive = false).
    /// Returns <c>true</c> if deleted, <c>false</c> if not found.
    /// </summary>
    /// <param name="id">Primary key of the workspace to delete.</param>
    Task<bool> DeleteWorkspaceAsync(Guid id);

    /// <summary>
    /// Restores a soft-deleted workspace (sets IsActive = true).
    /// Returns <c>true</c> if restored, <c>false</c> if not found.
    /// </summary>
    /// <param name="id">Primary key of the workspace to restore.</param>
    Task<bool> RestoreWorkspaceAsync(Guid id);

    /// <summary>
    /// Returns <c>true</c> if a workspace with the given primary key exists.
    /// </summary>
    /// <param name="id">Primary key to check.</param>
    Task<bool> WorkspaceExistsAsync(Guid id);

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

    /// <summary>
    /// Returns approved strategic plans for the workspace strategy dropdown.
    /// </summary>
    Task<IEnumerable<StrategicPlanDropdownItemDto>> GetStrategicPlansForDropdownAsync();

    /// <summary>
    /// Returns the regular (non-admin) members of a workspace.
    /// </summary>
    Task<IEnumerable<WorkspaceMemberDto>> GetMembersAsync(Guid workspaceId);

    /// <summary>
    /// Adds a regular member to a workspace. Returns the new member,
    /// or <c>null</c> if the workspace does not exist or the user is already a member.
    /// </summary>
    Task<WorkspaceMemberDto?> AddMemberAsync(Guid workspaceId, AddWorkspaceMemberDto dto, string? addedByUserId);

    /// <summary>
    /// Removes a regular member from a workspace.
    /// Returns <c>true</c> on success, <c>false</c> when the membership did not exist.
    /// </summary>
    Task<bool> RemoveMemberAsync(Guid workspaceId, string userId);

    /// <summary>
    /// Links or unlinks the strategic plan attached to a workspace.
    /// Returns the updated workspace, or <c>null</c> if not found.
    /// </summary>
    Task<WorkspaceResponseDto?> SetStrategyAsync(Guid workspaceId, Guid? strategicPlanId);
}
