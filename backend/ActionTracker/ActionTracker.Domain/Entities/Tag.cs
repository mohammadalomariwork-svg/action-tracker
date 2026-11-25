namespace ActionTracker.Domain.Entities;

/// <summary>
/// A free-text tag of one workspace (WO-2026-11 A-5). Created the first time
/// someone types the name on an action item of that workspace and reused as a
/// suggestion there only — a tag never belongs to two workspaces. Tags are
/// never deleted; a tag no item uses any more simply stays.
/// </summary>
public class Tag
{
    public Guid Id { get; set; }

    /// <summary>The workspace the tag belongs to.</summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>Display name (≤ 30), as first typed — trimmed, inner spaces collapsed.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Upper-case <see cref="Name"/>; unique per workspace, so matching ignores case.</summary>
    public string NormalizedName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }

    // Navigation properties
    public Workspace Workspace { get; set; } = null!;
    public ICollection<ActionItemTag> ActionItems { get; set; } = new List<ActionItemTag>();
}
