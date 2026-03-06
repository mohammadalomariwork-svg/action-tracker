namespace ActionTracker.Application.Features.Projects.Models;

/// <summary>
/// Indicates the urgency and importance of a project action item.
/// Used to help assignees and PMs triage their workload.
/// </summary>
public enum ActionItemPriority
{
    /// <summary>Can be addressed when capacity allows.</summary>
    Low = 1,

    /// <summary>Should be addressed in the normal course of work.</summary>
    Medium = 2,

    /// <summary>Requires prompt attention; may impact milestone delivery.</summary>
    High = 3,

    /// <summary>Must be resolved immediately; blocking progress or at risk of breach.</summary>
    Critical = 4
}
