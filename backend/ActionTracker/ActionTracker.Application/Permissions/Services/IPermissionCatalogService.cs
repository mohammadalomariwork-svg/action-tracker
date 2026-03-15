using ActionTracker.Application.Permissions.DTOs;

namespace ActionTracker.Application.Permissions.Services;

public interface IPermissionCatalogService
{
    // ── Areas ─────────────────────────────────────────────────────────────────
    Task<List<AppPermissionAreaDto>> GetAllAreasAsync();
    Task<AppPermissionAreaDto?>      GetAreaByIdAsync(Guid id);
    Task<AppPermissionAreaDto>       CreateAreaAsync(CreateAreaDto dto, string createdBy);
    Task<AppPermissionAreaDto?>      UpdateAreaAsync(Guid id, CreateAreaDto dto, string updatedBy);
    Task<bool>                       DeleteAreaAsync(Guid id, string deletedBy);

    // ── Actions ───────────────────────────────────────────────────────────────
    Task<List<AppPermissionActionDto>> GetAllActionsAsync();
    Task<AppPermissionActionDto?>      GetActionByIdAsync(Guid id);
    Task<AppPermissionActionDto>       CreateActionAsync(CreateActionDto dto, string createdBy);
    Task<AppPermissionActionDto?>      UpdateActionAsync(Guid id, CreateActionDto dto, string updatedBy);
    Task<bool>                         DeleteActionAsync(Guid id, string deletedBy);

    // ── Mappings ──────────────────────────────────────────────────────────────
    Task<List<AreaActionMappingDto>> GetAllMappingsAsync();
    Task<List<AreaActionMappingDto>> GetMappingsByAreaAsync(Guid areaId);
    Task<AreaActionMappingDto>       CreateMappingAsync(CreateAreaActionMappingDto dto, string createdBy);
    Task<bool>                       DeleteMappingAsync(Guid id, string deletedBy);
}
