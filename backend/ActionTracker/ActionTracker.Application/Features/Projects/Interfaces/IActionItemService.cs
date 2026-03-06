using System.Collections.Generic;
using System.Threading.Tasks;
using ActionTracker.Application.Features.Projects.DTOs;

namespace ActionTracker.Application.Features.Projects.Interfaces;

/// <summary>
/// Defines the application-level operations for managing action items across
/// workspaces, projects, and milestones.
/// </summary>
public interface IActionItemService
{
    /// <summary>
    /// Returns all standalone action items that belong to the specified
    /// workspace but are not associated with any project.
    /// </summary>
    /// <param name="workspaceId">Primary key of the workspace.</param>
    Task<IEnumerable<ActionItemListDto>> GetByWorkspaceAsync(int workspaceId);

    /// <summary>
    /// Returns all action items that belong to the specified project, including
    /// both project-level actions and those nested under individual milestones.
    /// </summary>
    /// <param name="projectId">Primary key of the project.</param>
    Task<IEnumerable<ActionItemListDto>> GetByProjectAsync(int projectId);

    /// <summary>
    /// Returns all action items assigned to the specified milestone.
    /// </summary>
    /// <param name="milestoneId">Primary key of the milestone.</param>
    Task<IEnumerable<ActionItemListDto>> GetByMilestoneAsync(int milestoneId);

    /// <summary>
    /// Returns the action item with the given primary key, including its
    /// attached documents and comments, or <c>null</c> if not found.
    /// </summary>
    /// <param name="id">Primary key of the action item.</param>
    Task<ActionItemDetailDto?> GetByIdAsync(int id);

    /// <summary>
    /// Creates a new action item from the supplied data.
    /// </summary>
    /// <param name="dto">Data for the new action item.</param>
    /// <returns>The newly created action item.</returns>
    Task<ActionItemDetailDto> CreateAsync(CreateActionItemDto dto);

    /// <summary>
    /// Updates the action item identified by <paramref name="id"/> with the
    /// supplied data.
    /// </summary>
    /// <param name="id">Primary key of the action item to update.</param>
    /// <param name="dto">Updated field values.</param>
    /// <returns>
    /// The updated action item, or <c>null</c> if not found.
    /// </returns>
    Task<ActionItemDetailDto?> UpdateAsync(int id, UpdateActionItemDto dto);

    /// <summary>
    /// Deletes the action item with the given primary key.
    /// </summary>
    /// <param name="id">Primary key of the action item to delete.</param>
    /// <returns>
    /// <c>true</c> if the record was found and deleted; <c>false</c> otherwise.
    /// </returns>
    Task<bool> DeleteAsync(int id);
}
