using ActionTracker.Application.Features.Milestones.DTOs;

namespace ActionTracker.Application.Features.Milestones.Interfaces;

public interface IMilestoneService
{
    Task<List<MilestoneResponseDto>> GetByProjectAsync(Guid projectId, CancellationToken ct);
    Task<MilestoneResponseDto?> GetByIdAsync(Guid milestoneId, CancellationToken ct);
    Task<MilestoneResponseDto> CreateAsync(Guid projectId, MilestoneCreateDto dto, CancellationToken ct);
    Task<MilestoneResponseDto> UpdateAsync(Guid projectId, Guid milestoneId, MilestoneUpdateDto dto, CancellationToken ct);
    Task DeleteAsync(Guid projectId, Guid milestoneId, CancellationToken ct);
    Task BaselineMilestonesAsync(Guid projectId, CancellationToken ct);
    Task<MilestoneStatsDto> GetProjectStatsAsync(Guid projectId, CancellationToken ct);
}
