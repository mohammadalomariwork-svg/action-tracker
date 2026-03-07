namespace ActionTracker.Application.Features.Projects.Models;

/// <summary>
/// Classifies a project as either day-to-day operational work or a
/// goal-aligned strategic initiative.
/// </summary>
public enum ProjectType
{
    /// <summary>Routine, ongoing operational work not tied to a strategic objective.</summary>
    Operational = 1,

    /// <summary>Initiative directly aligned to a <see cref="StrategicObjective"/>.</summary>
    Strategic = 2
}
