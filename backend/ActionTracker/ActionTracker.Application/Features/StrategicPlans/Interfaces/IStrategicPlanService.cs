using ActionTracker.Application.Features.StrategicPlans.DTOs;

namespace ActionTracker.Application.Features.StrategicPlans.Interfaces;

public interface IStrategicPlanService
{
    Task<StrategicPlanListResponseDto> GetAllAsync(
        int page,
        int pageSize,
        Guid? orgUnitId = null,
        int? status = null,
        int? year = null,
        bool includeDeleted = false,
        string? currentUserId = null,
        CancellationToken ct = default);

    Task<StrategicPlanDto?> GetByIdAsync(Guid id, string? currentUserId = null, CancellationToken ct = default);

    Task<List<StrategicPlanDto>> GetByOrgUnitAsync(Guid orgUnitId, string? currentUserId = null, CancellationToken ct = default);

    /// <summary>
    /// Returns the single plan whose status is Approved or PendingApproval for that org unit, or null.
    /// </summary>
    Task<StrategicPlanDto?> GetActiveForOrgUnitAsync(Guid orgUnitId, string? currentUserId = null, CancellationToken ct = default);

    Task<StrategicPlanDto> CreateAsync(CreateStrategicPlanRequestDto request, string userId, CancellationToken ct = default);

    Task<StrategicPlanDto> UpdateAsync(Guid id, UpdateStrategicPlanRequestDto request, string userId, CancellationToken ct = default);

    Task SoftDeleteAsync(Guid id, string userId, CancellationToken ct = default);

    Task RestoreAsync(Guid id, string userId, CancellationToken ct = default);
}
