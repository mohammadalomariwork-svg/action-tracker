namespace ActionTracker.Application.Features.Projects.Models;

/// <summary>
/// Tracks the execution state of a project milestone (work package).
/// </summary>
public enum MilestoneStatus
{
    /// <summary>Milestone has not yet been started.</summary>
    NotStarted = 1,

    /// <summary>Work on the milestone is currently underway.</summary>
    InProgress = 2,

    /// <summary>All milestone deliverables have been completed.</summary>
    Completed = 3,

    /// <summary>Milestone is behind schedule relative to its planned end date.</summary>
    Delayed = 4,

    /// <summary>Milestone has been formally cancelled.</summary>
    Cancelled = 5
}
