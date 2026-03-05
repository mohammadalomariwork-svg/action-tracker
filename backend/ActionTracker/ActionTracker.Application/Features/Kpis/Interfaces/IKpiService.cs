using ActionTracker.Application.Features.Kpis.DTOs;

namespace ActionTracker.Application.Features.Kpis.Interfaces;

public interface IKpiService
{
    /// <summary>Returns a paged list of KPIs, optionally filtered by strategic objective and including soft-deleted records.</summary>
    Task<KpiListResponseDto> GetAllAsync(int page, int pageSize, Guid? objectiveId = null, bool includeDeleted = false, CancellationToken ct = default);

    /// <summary>Returns a single KPI with its targets for the given year (all years if year is null). Returns null if not found.</summary>
    Task<KpiWithTargetsDto?> GetByIdAsync(Guid id, int? year = null, CancellationToken ct = default);

    /// <summary>Auto-assigns KpiNumber sequentially within the strategic objective and creates the record.</summary>
    Task<KpiDto> CreateAsync(CreateKpiRequestDto request, CancellationToken ct = default);

    /// <summary>Updates name, description, calculation method, period, and unit of an existing KPI.</summary>
    Task<KpiDto> UpdateAsync(Guid id, UpdateKpiRequestDto request, CancellationToken ct = default);

    /// <summary>Soft-deletes the KPI.</summary>
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Restores a soft-deleted KPI.</summary>
    Task RestoreAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all non-deleted KPIs belonging to the specified strategic objective.</summary>
    Task<List<KpiDto>> GetByObjectiveAsync(Guid objectiveId, CancellationToken ct = default);

    /// <summary>Upserts a single month target (insert if not exists, update if exists).</summary>
    Task<KpiTargetDto> UpsertTargetAsync(UpsertKpiTargetRequestDto request, CancellationToken ct = default);

    /// <summary>Saves all 12 months for a KPI/year combination in one transaction.</summary>
    Task<List<KpiTargetDto>> BulkUpsertTargetsAsync(BulkUpsertKpiTargetsRequestDto request, CancellationToken ct = default);

    /// <summary>Returns all targets for a KPI in the specified year.</summary>
    Task<List<KpiTargetDto>> GetTargetsAsync(Guid kpiId, int year, CancellationToken ct = default);
}
