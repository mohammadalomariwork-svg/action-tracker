namespace ActionTracker.Application.Features.ActionItems;

/// <summary>
/// The standalone flag on action items follows the project link (WO-2026-11 A-1b).
///
/// <list type="bullet">
/// <item>The client's <c>isStandalone</c> is ignored on create and on update.</item>
/// <item>On create the flag is true exactly when no project is given.</item>
/// <item>On update the flag is worked out again only when the save changes the
/// project link: linking sets it to false, clearing the link sets it to true.
/// A save that leaves the link alone keeps the stored flag, so an edit never
/// repairs or changes an existing row on the side.</item>
/// </list>
///
/// Before A-1b three edit screens sent <c>isStandalone: true</c> on every save,
/// which turned a project-linked item into a standalone one.
/// </summary>
public static class ActionItemStandalone
{
    /// <summary>True when <paramref name="projectId"/> names no project (null or <see cref="Guid.Empty"/>).</summary>
    public static bool ForProject(Guid? projectId) => projectId is null || projectId == Guid.Empty;

    /// <summary>
    /// The flag after an update: <paramref name="storedFlag"/> when the project
    /// link is unchanged, otherwise <see cref="ForProject"/> of the new link.
    /// </summary>
    public static bool AfterUpdate(bool storedFlag, Guid? previousProjectId, Guid? newProjectId) =>
        previousProjectId == newProjectId ? storedFlag : ForProject(newProjectId);
}
