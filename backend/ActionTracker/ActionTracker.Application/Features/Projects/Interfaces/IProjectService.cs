using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Helpers;

namespace ActionTracker.Application.Features.Projects.Interfaces;

public interface IProjectService
{
    Task<PagedResult<ProjectResponseDto>> GetAllAsync(ProjectFilterDto filter, CancellationToken ct);
    Task<ProjectResponseDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<ProjectResponseDto> CreateAsync(ProjectCreateDto dto, string userId, CancellationToken ct);
    Task<ProjectResponseDto> UpdateAsync(Guid id, ProjectUpdateDto dto, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<List<StrategicObjectiveOptionDto>> GetStrategicObjectivesForWorkspaceAsync(Guid workspaceId, CancellationToken ct);
}
