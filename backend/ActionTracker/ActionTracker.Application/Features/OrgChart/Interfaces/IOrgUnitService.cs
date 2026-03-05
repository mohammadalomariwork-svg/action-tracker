using ActionTracker.Application.Features.OrgChart.DTOs;

namespace ActionTracker.Application.Features.OrgChart.Interfaces;

public interface IOrgUnitService
{
    /// <summary>Returns full tree starting from root, including soft-deleted nodes if includeDeleted=true.</summary>
    Task<OrgUnitTreeDto> GetTreeAsync(bool includeDeleted = false, CancellationToken ct = default);

    /// <summary>Returns flat paged list of all org units.</summary>
    Task<OrgUnitListResponseDto> GetAllAsync(int page, int pageSize, bool includeDeleted = false, CancellationToken ct = default);

    /// <summary>Returns a single org unit by ID. Returns null if not found.</summary>
    Task<OrgUnitDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Creates a new org unit. Validates: max depth 10, ParentId exists, only one root.</summary>
    Task<OrgUnitDto> CreateAsync(CreateOrgUnitRequestDto request, CancellationToken ct = default);

    /// <summary>Updates name, description, code, and parentId. Validates no circular reference.</summary>
    Task<OrgUnitDto> UpdateAsync(Guid id, UpdateOrgUnitRequestDto request, CancellationToken ct = default);

    /// <summary>Soft-deletes the org unit and all its descendants recursively.</summary>
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Restores a soft-deleted org unit (not its descendants automatically).</summary>
    Task RestoreAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all direct children of a given org unit.</summary>
    Task<List<OrgUnitDto>> GetChildrenAsync(Guid parentId, CancellationToken ct = default);
}
