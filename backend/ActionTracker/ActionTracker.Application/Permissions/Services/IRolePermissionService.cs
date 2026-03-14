using ActionTracker.Application.Permissions.DTOs;

namespace ActionTracker.Application.Permissions.Services;

public interface IRolePermissionService
{
    /// <summary>Returns all active permissions for the given role.</summary>
    Task<List<RolePermissionDto>> GetAllByRoleAsync(string roleName);

    /// <summary>Returns a single permission by ID, or null if not found.</summary>
    Task<RolePermissionDto?> GetByIdAsync(Guid id);

    /// <summary>
    /// Creates a new role permission.
    /// Throws <see cref="ArgumentException"/> if an identical (role + area + action) already exists.
    /// </summary>
    Task<RolePermissionDto> CreateAsync(CreateRolePermissionDto dto, string createdByUserId);

    /// <summary>Updates the scope / active flag of an existing permission.</summary>
    Task<RolePermissionDto> UpdateAsync(Guid id, UpdateRolePermissionDto dto, string updatedByUserId);

    /// <summary>Soft-deletes a permission.</summary>
    Task DeleteAsync(Guid id, string deletedByUserId);

    /// <summary>Returns all active permissions for the role grouped into a matrix DTO.</summary>
    Task<PermissionMatrixDto> GetPermissionMatrixAsync(string roleName);
}
