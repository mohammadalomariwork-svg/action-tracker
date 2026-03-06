namespace ActionTracker.Application.Features.Projects.Models;

/// <summary>
/// Tracks the approval workflow state of a baseline change request.
/// Change requests follow a Sponsor-approval gate before the PM implements them.
/// </summary>
public enum ChangeRequestStatus
{
    /// <summary>Change request has been submitted and is awaiting Sponsor review.</summary>
    Pending = 1,

    /// <summary>Sponsor has reviewed and approved the change request.</summary>
    ApprovedBySponsor = 2,

    /// <summary>Sponsor has rejected the change request; no changes are made.</summary>
    Rejected = 3,

    /// <summary>PM has applied the approved changes to the project baseline.</summary>
    Implemented = 4
}
