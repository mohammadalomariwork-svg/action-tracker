using ActionTracker.Application.Features.Kpis.DTOs;

namespace ActionTracker.Application.Features.Kpis.Interfaces;

public interface IKpiService
{
    /// <summary>Returns a paged list of KPIs, optionally filtered by strategic objective. Scoped to the caller when <paramref name="currentUserId"/> is provided.</summary>
    Task<KpiListResponseDto> GetAllAsync(int page, int pageSize, Guid? objectiveId = null, bool includeDeleted = false, string? currentUserId = null, CancellationToken ct = default);

    /// <summary>Returns a single KPI with its targets, scoped to the caller when provided. Null when missing or out of scope.</summary>
    Task<KpiWithTargetsDto?> GetByIdAsync(Guid id, int? year = null, string? currentUserId = null, CancellationToken ct = default);

    /// <summary>Auto-assigns KpiNumber sequentially within the strategic objective and creates the record.</summary>
    Task<KpiDto> CreateAsync(CreateKpiRequestDto request, string userId, CancellationToken ct = default);

    /// <summary>Updates name, description, calculation method, period, and unit of an existing KPI.</summary>
    Task<KpiDto> UpdateAsync(Guid id, UpdateKpiRequestDto request, string userId, CancellationToken ct = default);

    /// <summary>Soft-deletes the KPI.</summary>
    Task SoftDeleteAsync(Guid id, string userId, CancellationToken ct = default);

    /// <summary>Restores a soft-deleted KPI.</summary>
    Task RestoreAsync(Guid id, string userId, CancellationToken ct = default);

    /// <summary>Returns all non-deleted KPIs belonging to the specified strategic objective, scoped to the caller when provided.</summary>
    Task<List<KpiDto>> GetByObjectiveAsync(Guid objectiveId, string? currentUserId = null, CancellationToken ct = default);

    /// <summary>Upserts a single month target (insert if not exists, update if exists).</summary>
    Task<KpiTargetDto> UpsertTargetAsync(UpsertKpiTargetRequestDto request, string userId, CancellationToken ct = default);

    /// <summary>Saves all 12 months for a KPI/year combination in one transaction.</summary>
    Task<List<KpiTargetDto>> BulkUpsertTargetsAsync(BulkUpsertKpiTargetsRequestDto request, string userId, CancellationToken ct = default);

    /// <summary>Returns all targets for a KPI in the specified year, scoped to the caller when provided.</summary>
    Task<List<KpiTargetDto>> GetTargetsAsync(Guid kpiId, int year, string? currentUserId = null, CancellationToken ct = default);
}
