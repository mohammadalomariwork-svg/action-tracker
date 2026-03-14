using ActionTracker.Application.Permissions.DTOs;

namespace ActionTracker.Application.Permissions.Services;

public interface IUserPermissionOverrideService
{
    /// <summary>Returns all active overrides for the given user.</summary>
    Task<List<UserPermissionOverrideDto>> GetAllByUserAsync(string userId);

    /// <summary>Returns a single override by ID, or null if not found.</summary>
    Task<UserPermissionOverrideDto?> GetByIdAsync(Guid id);

    /// <summary>
    /// Creates a new user-level permission override.
    /// Throws <see cref="ArgumentException"/> if an identical (user + area + action) already exists.
    /// </summary>
    Task<UserPermissionOverrideDto> CreateAsync(CreateUserPermissionOverrideDto dto, string createdByUserId);

    /// <summary>Updates scope, grant flag, reason, expiry and active flag of an existing override.</summary>
    Task<UserPermissionOverrideDto> UpdateAsync(Guid id, UpdateUserPermissionOverrideDto dto, string updatedByUserId);

    /// <summary>Soft-deletes an override.</summary>
    Task DeleteAsync(Guid id, string deletedByUserId);

    /// <summary>Returns only overrides that are active and have not yet expired.</summary>
    Task<List<UserPermissionOverrideDto>> GetActiveOverridesForUserAsync(string userId);
}
