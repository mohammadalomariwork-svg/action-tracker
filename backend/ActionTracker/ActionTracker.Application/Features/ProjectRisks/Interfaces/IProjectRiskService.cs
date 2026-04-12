using ActionTracker.Application.Features.ProjectRisks.DTOs;
using ActionTracker.Application.Helpers;

namespace ActionTracker.Application.Features.ProjectRisks.Interfaces;

public interface IProjectRiskService
{
    Task<PagedResult<ProjectRiskSummaryDto>> GetByProjectAsync(Guid projectId, int page, int pageSize, string? status, string? rating, string? category, CancellationToken ct = default);
    Task<ProjectRiskDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProjectRiskDto> CreateAsync(CreateProjectRiskDto dto, string userId, string userDisplayName, CancellationToken ct = default);
    Task<ProjectRiskDto> UpdateAsync(Guid id, UpdateProjectRiskDto dto, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
    Task RestoreAsync(Guid id, CancellationToken ct = default);
    Task<ProjectRiskStatsDto> GetStatsAsync(Guid projectId, CancellationToken ct = default);
}
