using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ActionTracker.Application.Features.Projects.DTOs;

namespace ActionTracker.Application.Features.Projects.Interfaces;

/// <summary>
/// Defines the application-level operations for managing milestones within a
/// project, including ordered-list management.
/// </summary>
public interface IMilestoneService
{
    /// <summary>
    /// Returns all milestones that belong to the specified project, ordered by
    /// their display order.
    /// </summary>
    /// <param name="projectId">Primary key of the parent project.</param>
    Task<IEnumerable<MilestoneListDto>> GetByProjectAsync(Guid projectId);

    /// <summary>
    /// Returns the milestone with the given primary key including its nested
    /// action items, or <c>null</c> if not found.
    /// </summary>
    /// <param name="id">Primary key of the milestone.</param>
    Task<MilestoneDetailDto?> GetByIdAsync(Guid id);

    /// <summary>
    /// Creates a new milestone from the supplied data.
    /// </summary>
    /// <param name="dto">Data for the new milestone.</param>
    /// <returns>The newly created milestone.</returns>
    Task<MilestoneDetailDto> CreateAsync(CreateMilestoneDto dto);

    /// <summary>
    /// Updates the milestone identified by <paramref name="id"/> with the
    /// supplied data.
    /// </summary>
    /// <param name="id">Primary key of the milestone to update.</param>
    /// <param name="dto">Updated field values.</param>
    /// <returns>
    /// The updated milestone, or <c>null</c> if not found.
    /// </returns>
    Task<MilestoneDetailDto?> UpdateAsync(Guid id, UpdateMilestoneDto dto);

    /// <summary>
    /// Deletes the milestone with the given primary key.
    /// </summary>
    /// <param name="id">Primary key of the milestone to delete.</param>
    /// <returns>
    /// <c>true</c> if the record was found and deleted; <c>false</c> otherwise.
    /// </returns>
    Task<bool> DeleteAsync(Guid id);

    /// <summary>
    /// Persists the display order of all milestones within a project based on
    /// the caller-supplied ordered list of IDs.
    /// </summary>
    /// <param name="projectId">Primary key of the parent project.</param>
    /// <param name="orderedMilestoneIds">
    /// Milestone primary keys in the desired display order.  All milestones
    /// belonging to the project must be present.
    /// </param>
    /// <returns>
    /// <c>true</c> if the reorder succeeded; <c>false</c> if the project or
    /// any ID was not found.
    /// </returns>
    Task<bool> ReorderMilestonesAsync(Guid projectId, List<Guid> orderedMilestoneIds);
}
