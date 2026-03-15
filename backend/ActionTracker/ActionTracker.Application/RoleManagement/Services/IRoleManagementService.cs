using ActionTracker.Application.Permissions.DTOs;
using ActionTracker.Application.RoleManagement.DTOs;

namespace ActionTracker.Application.RoleManagement.Services;

public interface IRoleManagementService
{
    Task<List<AppRoleDto>>  GetAllRolesAsync();
    Task<AppRoleDto?>       GetRoleByNameAsync(string roleName);

    /// <summary>Creates a new Identity role. Throws <see cref="ArgumentException"/> if the name already exists.</summary>
    Task<AppRoleDto>        CreateRoleAsync(string roleName, string createdByUserId);

    /// <summary>
    /// Soft-deletes the role. Throws <see cref="InvalidOperationException"/> if users are still assigned.
    /// Returns false when the role does not exist.
    /// </summary>
    Task<bool>              DeleteRoleAsync(string roleName, string deletedByUserId);

    Task<List<RoleUserDto>> GetUsersInRoleAsync(string roleName);
    Task<bool>              AssignUsersToRoleAsync(AssignUsersToRoleDto dto, string actingUserId);
    Task<bool>              RemoveUsersFromRoleAsync(RemoveUsersFromRoleDto dto, string actingUserId);

    /// <summary>
    /// Full replace: soft-deletes all existing RolePermissions for the role,
    /// then bulk-inserts the provided permission entries.
    /// </summary>
    Task<bool>              AssignPermissionsToRoleAsync(AssignRolePermissionsDto dto, string actingUserId);

    Task<PermissionMatrixDto> GetRolePermissionSummaryAsync(string roleName);
}
