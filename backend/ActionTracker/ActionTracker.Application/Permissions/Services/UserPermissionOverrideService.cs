using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Permissions.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Permissions.Services;

public class UserPermissionOverrideService : IUserPermissionOverrideService
{
    private readonly IAppDbContext _db;
    private readonly ILogger<UserPermissionOverrideService> _logger;

    public UserPermissionOverrideService(IAppDbContext db, ILogger<UserPermissionOverrideService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<List<UserPermissionOverrideDto>> GetAllByUserAsync(string userId)
    {
        var entities = await _db.UserPermissionOverrides
            .Where(o => o.UserId == userId && !o.IsDeleted)
            .OrderBy(o => o.AreaName).ThenBy(o => o.ActionName)
            .ToListAsync();

        return entities.Select(MapToDto).ToList();
    }

    public async Task<UserPermissionOverrideDto?> GetByIdAsync(Guid id)
    {
        var entity = await _db.UserPermissionOverrides.FirstOrDefaultAsync(o => o.Id == id);
        return entity is null ? null : MapToDto(entity);
    }

    public async Task<List<UserPermissionOverrideDto>> GetActiveOverridesForUserAsync(string userId)
    {
        var now = DateTime.UtcNow;
        var entities = await _db.UserPermissionOverrides
            .Where(o => o.UserId == userId
                     && o.IsActive
                     && !o.IsDeleted
                     && (o.ExpiresAt == null || o.ExpiresAt > now))
            .OrderBy(o => o.AreaName).ThenBy(o => o.ActionName)
            .ToListAsync();

        return entities.Select(MapToDto).ToList();
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public async Task<UserPermissionOverrideDto> CreateAsync(
        CreateUserPermissionOverrideDto dto, string createdByUserId)
    {
        var area = await _db.PermissionAreas
            .FirstOrDefaultAsync(a => a.Id == dto.AreaId && a.IsActive)
            ?? throw new ArgumentException($"Area '{dto.AreaId}' does not exist or is not active.");

        var action = await _db.PermissionActions
            .FirstOrDefaultAsync(a => a.Id == dto.ActionId && a.IsActive)
            ?? throw new ArgumentException($"Action '{dto.ActionId}' does not exist or is not active.");

        var duplicate = await _db.UserPermissionOverrides
            .AnyAsync(o => o.UserId   == dto.UserId
                        && o.AreaId   == dto.AreaId
                        && o.ActionId == dto.ActionId
                        && !o.IsDeleted);

        if (duplicate)
            throw new ArgumentException(
                $"An override for user '{dto.UserId}', area '{area.Name}', action '{action.Name}' already exists.");

        var entity = new UserPermissionOverride
        {
            Id              = Guid.NewGuid(),
            UserId          = dto.UserId,
            UserDisplayName = dto.UserDisplayName,
            AreaId          = area.Id,
            AreaName        = area.Name,
            ActionId        = action.Id,
            ActionName      = action.Name,
            OrgUnitScope    = dto.OrgUnitScope,
            OrgUnitId       = dto.OrgUnitId,
            OrgUnitName     = dto.OrgUnitName,
            IsGranted       = dto.IsGranted,
            Reason          = dto.Reason,
            ExpiresAt       = dto.ExpiresAt,
            IsActive        = true,
            CreatedAt       = DateTime.UtcNow,
            CreatedBy       = createdByUserId,
        };

        _db.UserPermissionOverrides.Add(entity);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "UserPermissionOverride {Id} created for user '{UserId}' area '{Area}' action '{Action}' " +
            "IsGranted={IsGranted} by {CreatedBy}",
            entity.Id, entity.UserId, entity.AreaName, entity.ActionName, entity.IsGranted, createdByUserId);

        return MapToDto(entity);
    }

    public async Task<UserPermissionOverrideDto> UpdateAsync(
        Guid id, UpdateUserPermissionOverrideDto dto, string updatedByUserId)
    {
        var entity = await _db.UserPermissionOverrides.FirstOrDefaultAsync(o => o.Id == id)
                     ?? throw new KeyNotFoundException($"UserPermissionOverride {id} not found.");

        entity.OrgUnitScope = dto.OrgUnitScope;
        entity.OrgUnitId    = dto.OrgUnitId;
        entity.OrgUnitName  = dto.OrgUnitName;
        entity.IsGranted    = dto.IsGranted;
        entity.Reason       = dto.Reason;
        entity.ExpiresAt    = dto.ExpiresAt;
        entity.IsActive     = dto.IsActive;
        entity.UpdatedAt    = DateTime.UtcNow;
        entity.UpdatedBy    = updatedByUserId;

        await _db.SaveChangesAsync();

        _logger.LogInformation("UserPermissionOverride {Id} updated by {User}", id, updatedByUserId);

        return MapToDto(entity);
    }

    public async Task DeleteAsync(Guid id, string deletedByUserId)
    {
        var entity = await _db.UserPermissionOverrides.FirstOrDefaultAsync(o => o.Id == id)
                     ?? throw new KeyNotFoundException($"UserPermissionOverride {id} not found.");

        entity.IsDeleted = true;
        entity.IsActive  = false;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = deletedByUserId;

        await _db.SaveChangesAsync();

        _logger.LogInformation("UserPermissionOverride {Id} soft-deleted by {User}", id, deletedByUserId);
    }

    // ── Mapper ────────────────────────────────────────────────────────────────

    private static UserPermissionOverrideDto MapToDto(UserPermissionOverride o) => new()
    {
        Id              = o.Id,
        UserId          = o.UserId,
        UserDisplayName = o.UserDisplayName,
        AreaId          = o.AreaId,
        AreaName        = o.AreaName,
        ActionId        = o.ActionId,
        ActionName      = o.ActionName,
        OrgUnitScope    = o.OrgUnitScope,
        OrgUnitId       = o.OrgUnitId,
        OrgUnitName     = o.OrgUnitName,
        IsGranted       = o.IsGranted,
        Reason          = o.Reason,
        ExpiresAt       = o.ExpiresAt,
        IsActive        = o.IsActive,
        CreatedAt       = o.CreatedAt,
    };
}
