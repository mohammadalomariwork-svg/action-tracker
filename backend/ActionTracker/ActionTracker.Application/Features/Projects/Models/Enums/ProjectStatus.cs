namespace ActionTracker.Application.Features.Projects.Models;

/// <summary>
/// Tracks the lifecycle stage of a project from inception to closure.
/// </summary>
public enum ProjectStatus
{
    /// <summary>Project has been created but not yet formally activated.</summary>
    Draft = 1,

    /// <summary>Project is actively in progress.</summary>
    Active = 2,

    /// <summary>Project has been temporarily paused.</summary>
    OnHold = 3,

    /// <summary>Project has been successfully delivered and closed.</summary>
    Completed = 4,

    /// <summary>Project has been formally cancelled.</summary>
    Cancelled = 5
}
