using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ActionTracker.Application.Features.Projects.DTOs;

namespace ActionTracker.Application.Features.Projects.Interfaces;

/// <summary>
/// Defines the application-level operations for managing projects within a
/// workspace, including lifecycle management, baselining, and full-detail
/// retrieval.
/// </summary>
public interface IProjectService
{
    /// <summary>
    /// Returns a lightweight list of all projects that belong to the specified
    /// workspace, suitable for dashboard and list views.
    /// </summary>
    /// <param name="workspaceId">Primary key of the workspace.</param>
    Task<IEnumerable<ProjectListDto>> GetByWorkspaceAsync(Guid workspaceId);

    /// <summary>
    /// Returns the project with the given primary key, or <c>null</c> if not
    /// found. Includes standard detail fields but not nested collections.
    /// </summary>
    /// <param name="id">Primary key of the project.</param>
    Task<ProjectDetailDto?> GetByIdAsync(int id);

    /// <summary>
    /// Creates a new project from the supplied data.
    /// </summary>
    /// <param name="dto">Data for the new project.</param>
    /// <returns>The newly created project.</returns>
    Task<ProjectDetailDto> CreateAsync(CreateProjectDto dto);

    /// <summary>
    /// Updates the project identified by <paramref name="id"/> with the
    /// supplied data.
    /// </summary>
    /// <param name="id">Primary key of the project to update.</param>
    /// <param name="dto">Updated field values.</param>
    /// <returns>
    /// The updated project, or <c>null</c> if not found.
    /// </returns>
    Task<ProjectDetailDto?> UpdateAsync(int id, UpdateProjectDto dto);

    /// <summary>
    /// Soft-deletes the project with the given primary key.
    /// The record is retained in the database with its <c>IsActive</c> flag
    /// set to <c>false</c>.
    /// </summary>
    /// <param name="id">Primary key of the project to soft-delete.</param>
    /// <returns>
    /// <c>true</c> if the record was found and soft-deleted; <c>false</c>
    /// otherwise.
    /// </returns>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Creates an immutable baseline snapshot of the project's current schedule
    /// and scope.  After baselining, schedule changes must go through the
    /// change-request workflow.
    /// </summary>
    /// <param name="projectId">Primary key of the project to baseline.</param>
    /// <param name="baselinedByUserId">
    /// AspNetUsers.Id of the user performing the baselining.
    /// </param>
    /// <param name="baselinedByUserName">
    /// Display name of the user performing the baselining.
    /// </param>
    /// <returns>The newly created baseline record.</returns>
    Task<ProjectBaselineDto> BaselineProjectAsync(
        int projectId,
        string baselinedByUserId,
        string baselinedByUserName);

    /// <summary>
    /// Removes the frozen/baselined state from a project after an approved
    /// baseline change request has been implemented, allowing schedule edits
    /// again.
    /// </summary>
    /// <param name="projectId">Primary key of the project to unfreeze.</param>
    /// <returns>
    /// <c>true</c> if the project was found and unfrozen; <c>false</c>
    /// otherwise.
    /// </returns>
    Task<bool> UnfreezeProjectAsync(int projectId);

    /// <summary>
    /// Returns the project with the given primary key together with its full
    /// nested collections — milestones, action items, budget record, and
    /// contracts.  Use for detail pages that require all associated data in a
    /// single round-trip.
    /// </summary>
    /// <param name="id">Primary key of the project.</param>
    /// <returns>
    /// The fully-hydrated project, or <c>null</c> if not found.
    /// </returns>
    Task<ProjectDetailDto?> GetProjectWithFullDetailsAsync(int id);
}
