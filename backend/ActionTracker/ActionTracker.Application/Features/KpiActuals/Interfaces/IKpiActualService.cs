using ActionTracker.Application.Features.KpiActuals.DTOs;

namespace ActionTracker.Application.Features.KpiActuals.Interfaces;

/// <summary>
/// CR-001-F — period-aware CRUD for <c>KpiActual</c> entities. Workflow
/// transitions (Submit / Review) live in <see cref="IKpiActualWorkflowService"/>.
/// </summary>
public interface IKpiActualService
{
    Task<KpiActualDto> CreateAsync(CreateKpiActualRequestDto dto, string currentUserId, CancellationToken ct = default);
    Task<KpiActualDto> UpdateAsync(Guid actualId, UpdateKpiActualRequestDto dto, string currentUserId, CancellationToken ct = default);
    Task DeleteAsync(Guid actualId, string currentUserId, CancellationToken ct = default);
    Task<KpiActualDto?> GetByIdAsync(Guid actualId, string currentUserId, CancellationToken ct = default);

    /// <summary>Returns one row per period slot for the given year (history-aware).</summary>
    Task<KpiActualGridResponseDto> GetGridAsync(Guid kpiTargetId, int year, string currentUserId, CancellationToken ct = default);

    /// <summary>Returns every KpiActual for a KPI in the given year (all targets, all statuses including history).</summary>
    Task<List<KpiActualDto>> ListByKpiAsync(Guid kpiId, int year, string currentUserId, CancellationToken ct = default);

    /// <summary>History (rejected + approved + draft) for a single (target, period, year) slot.</summary>
    Task<List<KpiActualDto>> GetHistoryAsync(Guid kpiTargetId, int periodType, int periodIndex, int year, string currentUserId, CancellationToken ct = default);
}
