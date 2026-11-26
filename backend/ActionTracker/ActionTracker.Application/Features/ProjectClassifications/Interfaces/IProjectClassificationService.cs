using ActionTracker.Application.Features.ProjectClassifications.DTOs;

namespace ActionTracker.Application.Features.ProjectClassifications.Interfaces;

/// <summary>
/// Internal project classifications of a workspace (WO-2026-11 P-1).
/// <para>
/// Visibility: every call first applies the private-workspace query filter.
/// Reads also need the caller to see the workspace through
/// <c>IWorkspaceAccessResolver</c> <b>or</b> to be on its Project Managers list
/// (they pick the classification on their projects); writes need the resolver.
/// Otherwise <see cref="KeyNotFoundException"/> (404).
/// </para>
/// Errors: <see cref="ArgumentException"/> (400) for a broken name rule or a bad
/// reorder list; <see cref="KeyNotFoundException"/> (404) for a hidden workspace
/// or a classification that is not one of its live ones;
/// <see cref="ProjectClassificationConflictException"/> (409) for a duplicate name
/// or a delete while projects use it.
/// </summary>
public interface IProjectClassificationService
{
    /// <summary>Every live classification of the workspace (active and inactive) by sort order, with usage counts.</summary>
    Task<List<ProjectClassificationDto>> GetForWorkspaceAsync(Guid workspaceId, string userId, CancellationToken ct = default);

    Task<ProjectClassificationDto> CreateAsync(Guid workspaceId, CreateProjectClassificationDto dto, string userId, CancellationToken ct = default);

    Task<ProjectClassificationDto> UpdateAsync(Guid workspaceId, Guid classificationId, UpdateProjectClassificationDto dto, string userId, CancellationToken ct = default);

    /// <summary>New order; the list must hold every live classification exactly once.</summary>
    Task<List<ProjectClassificationDto>> ReorderAsync(Guid workspaceId, ReorderProjectClassificationsDto dto, string userId, CancellationToken ct = default);

    /// <summary>Soft delete; refused (409) while any project — soft-deleted ones included — uses it.</summary>
    Task DeleteAsync(Guid workspaceId, Guid classificationId, string userId, CancellationToken ct = default);

    /// <summary>
    /// The rule for a project's classification, used by <c>ProjectService</c> on
    /// create and update. <paramref name="requestedId"/> is the classification the
    /// save asks for (never null here); <paramref name="currentId"/> the one the
    /// project holds now (null on create). The same id as now is always accepted,
    /// inactive or not. Anything else must be a live classification of
    /// <paramref name="workspaceId"/> that the caller can see and that is active —
    /// else <see cref="ArgumentException"/> (400), without naming another
    /// workspace's classification.
    /// </summary>
    Task EnsureAssignableAsync(Guid workspaceId, Guid requestedId, Guid? currentId, CancellationToken ct = default);
}
