using ActionTracker.Application.Common.Extensions;
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
            .Where(o => o.UserId == userId && o.IsActive)
            .OrderBy(o => o.Area).ThenBy(o => o.Action)
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
                     && (o.ExpiresAt == null || o.ExpiresAt > now))
            .OrderBy(o => o.Area).ThenBy(o => o.Action)
            .ToListAsync();

        return entities.Select(MapToDto).ToList();
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public async Task<UserPermissionOverrideDto> CreateAsync(
        CreateUserPermissionOverrideDto dto, string createdByUserId)
    {
        var area   = (PermissionArea)  dto.Area;
        var action = (PermissionAction)dto.Action;

        var duplicate = await _db.UserPermissionOverrides
            .AnyAsync(o => o.UserId == dto.UserId
                        && o.Area   == area
                        && o.Action == action);

        if (duplicate)
            throw new ArgumentException(
                $"An override for user '{dto.UserId}', area '{area}', action '{action}' already exists.");

        var entity = new UserPermissionOverride
        {
            Id              = Guid.NewGuid(),
            UserId          = dto.UserId,
            UserDisplayName = dto.UserDisplayName,
            Area            = area,
            Action          = action,
            OrgUnitScope    = (OrgUnitScope)dto.OrgUnitScope,
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
            entity.Id, entity.UserId, entity.Area, entity.Action, entity.IsGranted, createdByUserId);

        return MapToDto(entity);
    }

    public async Task<UserPermissionOverrideDto> UpdateAsync(
        Guid id, UpdateUserPermissionOverrideDto dto, string updatedByUserId)
    {
        var entity = await _db.UserPermissionOverrides.FirstOrDefaultAsync(o => o.Id == id)
                     ?? throw new KeyNotFoundException($"UserPermissionOverride {id} not found.");

        entity.OrgUnitScope = (OrgUnitScope)dto.OrgUnitScope;
        entity.OrgUnitId    = dto.OrgUnitId;
        entity.OrgUnitName  = dto.OrgUnitName;
        entity.IsGranted    = dto.IsGranted;
        entity.Reason       = dto.Reason;
        entity.ExpiresAt    = dto.ExpiresAt;
        entity.IsActive     = dto.IsActive;
        entity.UpdatedAt    = DateTime.UtcNow;
        entity.UpdatedBy    = updatedByUserId;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "UserPermissionOverride {Id} updated by {User}", id, updatedByUserId);

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

        _logger.LogInformation(
            "UserPermissionOverride {Id} soft-deleted by {User}", id, deletedByUserId);
    }

    // ── Mapper ────────────────────────────────────────────────────────────────

    private static UserPermissionOverrideDto MapToDto(UserPermissionOverride o) => new()
    {
        Id              = o.Id,
        UserId          = o.UserId,
        UserDisplayName = o.UserDisplayName,
        Area            = o.Area.GetDescription(),
        Action          = o.Action.GetDescription(),
        OrgUnitScope    = o.OrgUnitScope.GetDescription(),
        OrgUnitId       = o.OrgUnitId,
        OrgUnitName     = o.OrgUnitName,
        IsGranted       = o.IsGranted,
        Reason          = o.Reason,
        ExpiresAt       = o.ExpiresAt,
        IsActive        = o.IsActive,
        CreatedAt       = o.CreatedAt,
    };
}
