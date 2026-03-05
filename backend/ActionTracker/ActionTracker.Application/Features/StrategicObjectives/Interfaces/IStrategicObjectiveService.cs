using ActionTracker.Application.Features.StrategicObjectives.DTOs;

namespace ActionTracker.Application.Features.StrategicObjectives.Interfaces;

public interface IStrategicObjectiveService
{
    /// <summary>Returns a paged list of strategic objectives, optionally filtered by org unit and including soft-deleted records.</summary>
    Task<StrategicObjectiveListResponseDto> GetAllAsync(int page, int pageSize, Guid? orgUnitId = null, bool includeDeleted = false, CancellationToken ct = default);

    /// <summary>Returns a single strategic objective by ID. Returns null if not found.</summary>
    Task<StrategicObjectiveDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Auto-generates ObjectiveCode as SO-{next sequential number} and creates the record.</summary>
    Task<StrategicObjectiveDto> CreateAsync(CreateStrategicObjectiveRequestDto request, CancellationToken ct = default);

    /// <summary>Updates statement, description, and org unit assignment of an existing objective.</summary>
    Task<StrategicObjectiveDto> UpdateAsync(Guid id, UpdateStrategicObjectiveRequestDto request, CancellationToken ct = default);

    /// <summary>Soft-deletes the strategic objective.</summary>
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Restores a soft-deleted strategic objective.</summary>
    Task RestoreAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all non-deleted strategic objectives belonging to the specified org unit.</summary>
    Task<List<StrategicObjectiveDto>> GetByOrgUnitAsync(Guid orgUnitId, CancellationToken ct = default);
}
