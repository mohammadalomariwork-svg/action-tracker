using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ActionTracker.Application.Features.Projects.DTOs;

namespace ActionTracker.Application.Features.Projects.Interfaces;

/// <summary>
/// Defines the application-level operations for managing project baselines and
/// the change-request workflow that governs baseline modifications.
/// The workflow is: PM submits → Sponsor approves/rejects → PM implements if
/// approved.
/// </summary>
public interface IBaselineService
{
    /// <summary>
    /// Returns the baseline snapshot for the specified project, or <c>null</c>
    /// if the project has not yet been baselined.
    /// </summary>
    /// <param name="projectId">Primary key of the project.</param>
    Task<ProjectBaselineDto?> GetBaselineByProjectAsync(Guid projectId);

    /// <summary>
    /// Creates an immutable baseline snapshot of the project's current schedule
    /// and scope.
    /// </summary>
    /// <param name="projectId">Primary key of the project to baseline.</param>
    /// <param name="userId">
    /// AspNetUsers.Id of the user performing the baselining action.
    /// </param>
    /// <param name="userName">
    /// Display name of the user performing the baselining action.
    /// </param>
    /// <returns>The newly created baseline record.</returns>
    Task<ProjectBaselineDto> CreateBaselineAsync(Guid projectId, string userId, string userName);

    /// <summary>
    /// Returns all baseline change requests that have been submitted for the
    /// specified project, ordered by creation time descending.
    /// </summary>
    /// <param name="projectId">Primary key of the project.</param>
    Task<IEnumerable<BaselineChangeRequestDto>> GetChangeRequestsByProjectAsync(Guid projectId);

    /// <summary>
    /// Submits a new baseline change request on behalf of the project manager.
    /// The request is created with <c>Pending</c> status and awaits Sponsor
    /// review.
    /// </summary>
    /// <param name="dto">Change justification, proposed changes JSON, and requester details.</param>
    /// <returns>The newly created change request.</returns>
    Task<BaselineChangeRequestDto> SubmitChangeRequestAsync(CreateBaselineChangeRequestDto dto);

    /// <summary>
    /// Records the Sponsor's decision on a pending change request.
    /// Only <c>ApprovedBySponsor</c> or <c>Rejected</c> are valid status
    /// transitions — any other value will be rejected by this method.
    /// Only the project's designated Sponsor may call this method.
    /// </summary>
    /// <param name="dto">
    /// Reviewer identity, decision status, and optional review notes.
    /// </param>
    /// <returns>
    /// The updated change request, or <c>null</c> if the change request was
    /// not found.
    /// </returns>
    Task<BaselineChangeRequestDto?> ReviewChangeRequestAsync(ReviewChangeRequestDto dto);

    /// <summary>
    /// Marks an approved change request as <c>Implemented</c> after the PM
    /// has applied the approved changes to the project schedule.
    /// Also unfreezes the project to allow further edits.
    /// </summary>
    /// <param name="changeRequestId">
    /// Primary key of the change request to mark as implemented.
    /// Must be in <c>ApprovedBySponsor</c> status.
    /// </param>
    /// <param name="implementedByUserId">
    /// AspNetUsers.Id of the user applying the changes (typically the PM).
    /// </param>
    /// <returns>
    /// <c>true</c> if the change request was found and marked implemented;
    /// <c>false</c> otherwise.
    /// </returns>
    Task<bool> ImplementApprovedChangeAsync(Guid changeRequestId, string implementedByUserId);
}
