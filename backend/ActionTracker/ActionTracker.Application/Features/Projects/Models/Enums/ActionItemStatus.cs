namespace ActionTracker.Application.Features.Projects.Models;

/// <summary>
/// Tracks the execution state of a project or milestone action item.
/// </summary>
public enum ActionItemStatus
{
    /// <summary>Action has not yet been started.</summary>
    NotStarted = 1,

    /// <summary>Action is currently being worked on.</summary>
    InProgress = 2,

    /// <summary>Action has been completed.</summary>
    Completed = 3,

    /// <summary>Action has been pushed to a future date without cancellation.</summary>
    Deferred = 4,

    /// <summary>Action has been formally cancelled.</summary>
    Cancelled = 5
}
