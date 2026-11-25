namespace ActionTracker.Application.Features.Tags.DTOs;

/// <summary>A tag on an action item (WO-2026-11 A-5). Always of the item's own workspace.</summary>
public class TagDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// A tag of one workspace — suggestions for the tag input and the options of
/// the workspace action-item list's tag filter
/// (<c>GET /api/workspaces/{id}/tags</c>).
/// </summary>
public class WorkspaceTagDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Live action items the caller can see that carry the tag.</summary>
    public int ItemCount { get; set; }
}

/// <summary>
/// A filter option of My Actions (<c>GET /api/action-items/my-actions/tags</c>,
/// D3): a tag on an item assigned to the caller, labelled with its workspace.
/// The filter goes by <see cref="Id"/>, so the same name in two workspaces is
/// two options and never matches the other workspace's items.
/// </summary>
public class MyActionTagOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid WorkspaceId { get; set; }
    public string WorkspaceTitle { get; set; } = string.Empty;

    /// <summary>Live items assigned to the caller that carry the tag.</summary>
    public int ItemCount { get; set; }
}
