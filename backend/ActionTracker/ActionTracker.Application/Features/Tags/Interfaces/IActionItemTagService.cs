using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.Tags.DTOs;
using ActionTracker.Domain.Entities;

namespace ActionTracker.Application.Features.Tags.Interfaces;

/// <summary>
/// Tags on action items (WO-2026-11 A-5). Tags belong to one workspace: names
/// are only ever looked up in the item's own workspace, and every read goes
/// through the private-workspace query filters. Used by
/// <c>ActionItemService</c> (save, read, filter) and <c>TagsController</c>.
/// </summary>
public interface IActionItemTagService
{
    /// <summary>False while the tag tables are missing (migration pending).</summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>Throws <see cref="TagsUnavailableException"/> (503) while the tag tables are missing.</summary>
    Task EnsureAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// The tags of <paramref name="workspaceId"/> for already cleaned names, in
    /// the order given; names the workspace does not have yet are created
    /// there (and saved — call before any other change is tracked). A name
    /// that matches an existing tag regardless of case reuses that tag.
    /// </summary>
    Task<IReadOnlyList<Tag>> ResolveAsync(
        Guid workspaceId, IReadOnlyList<string> cleanedNames, string? userId, CancellationToken ct = default);

    /// <summary>The names of the tags on an item (for carrying them over to another workspace, D2).</summary>
    Task<IReadOnlyList<string>> GetItemTagNamesAsync(Guid actionItemId, CancellationToken ct = default);

    /// <summary>
    /// Makes <paramref name="item"/> carry exactly <paramref name="tags"/>:
    /// tracked changes only — the caller saves.
    /// </summary>
    Task ReplaceItemTagsAsync(ActionItem item, IReadOnlyList<Tag> tags, CancellationToken ct = default);

    /// <summary>Fills <c>Tags</c> of action-item responses (one query per call; nothing while unavailable).</summary>
    Task FillTagsAsync(IReadOnlyCollection<ActionItemResponseDto> items, CancellationToken ct = default);

    /// <summary>
    /// Suggestions / filter options of one workspace. <see cref="KeyNotFoundException"/>
    /// when the caller cannot see the workspace (private, or not one of theirs).
    /// </summary>
    Task<List<WorkspaceTagDto>> GetWorkspaceTagsAsync(
        Guid workspaceId, string userId, string? search, int limit, CancellationToken ct = default);

    /// <summary>My Actions filter options: the tags on live items assigned to the caller, with their workspace.</summary>
    Task<List<MyActionTagOptionDto>> GetMyActionTagsAsync(string userId, CancellationToken ct = default);
}
