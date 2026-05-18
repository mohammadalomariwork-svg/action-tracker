using System;
using ActionTracker.Application.Features.ActionItems.DTOs;
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
    /// Returns active users eligible to be assigned to an action item in the
    /// given workspace. The set is the union of workspace admins, regular
    /// members, and project managers — i.e. everyone with a direct seat in the
    /// workspace. Used by the action-item create/edit form to scope the
    /// Assignee(s) picker to workspace people instead of every user in the system.
    /// </summary>
    Task<IEnumerable<AssignableUserDto>> GetAssignableUsersAsync(Guid workspaceId);

    /// <summary>
    /// Bulk-adds every active user currently assigned to the given org unit
    /// (ApplicationUser.OrgUnitId == orgUnitId) as a workspace member.
    /// Users who are already members are silently skipped and reported in the
    /// SkippedAlreadyMembers counter. Returns null if the workspace or org unit
    /// does not exist.
    /// </summary>
    Task<AddMembersFromOrgUnitResultDto?> AddMembersFromOrgUnitAsync(
        Guid workspaceId, Guid orgUnitId, string? addedByUserId);

    /// <summary>
    /// Links or unlinks the strategic plan attached to a workspace.
    /// Returns the updated workspace, or <c>null</c> if not found.
    /// </summary>
    Task<WorkspaceResponseDto?> SetStrategyAsync(Guid workspaceId, Guid? strategicPlanId);

    // ── Project Managers (gating list for project create/edit) ───────────────

    Task<IEnumerable<WorkspaceProjectManagerDto>> GetProjectManagersAsync(Guid workspaceId);

    Task<WorkspaceProjectManagerDto?> AddProjectManagerAsync(
        Guid workspaceId, AddWorkspaceProjectManagerDto dto, string? addedByUserId);

    Task<bool> RemoveProjectManagerAsync(Guid workspaceId, string userId);

    /// <summary>Returns true if the user is on the workspace's project-manager list.</summary>
    Task<bool> IsProjectManagerAsync(Guid workspaceId, string userId);

    // ── Strategic Project Approvers (sequential chain) ───────────────────────

    Task<IEnumerable<WorkspaceStrategicProjectApproverDto>> GetStrategicProjectApproversAsync(Guid workspaceId);

    Task<WorkspaceStrategicProjectApproverDto?> AddStrategicProjectApproverAsync(
        Guid workspaceId, AddWorkspaceStrategicProjectApproverDto dto, string? addedByUserId);

    Task<bool> RemoveStrategicProjectApproverAsync(Guid workspaceId, string userId);

    /// <summary>
    /// Replaces the workspace's strategic-project approver chain in the order given.
    /// Order is 1-based and dense. Returns false if the workspace does not exist or
    /// if any of the UserIds is not currently an approver on this workspace.
    /// </summary>
    Task<bool> ReorderStrategicProjectApproversAsync(
        Guid workspaceId, List<string> orderedUserIds);
}
