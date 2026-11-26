namespace ActionTracker.Domain.Entities;

/// <summary>
/// An internal project classification defined by one workspace (WO-2026-11 P-1),
/// next to the global <c>ProjectType</c> (Operational / Strategic), which it does
/// not replace. Workspace managers add, rename, reorder and deactivate them; a
/// project carries at most one, always of its own workspace
/// (<see cref="Project.InternalClassificationId"/>). Deleting is a soft delete and
/// is refused while any project — soft-deleted ones included — uses it. A
/// deactivated classification stays on the projects that have it but cannot be
/// newly chosen.
/// </summary>
public class WorkspaceProjectClassification
{
    public Guid Id { get; set; }

    /// <summary>The workspace the classification belongs to; it never moves.</summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>Display name (≤ 100) — trimmed, inner white space collapsed.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Upper-case <see cref="Name"/>; unique per workspace among live rows, so names ignore case.</summary>
    public string NormalizedName { get; set; } = string.Empty;

    /// <summary>1-based position in the workspace's list, dense among live rows.</summary>
    public int SortOrder { get; set; }

    /// <summary>False = kept on projects that have it, but not offered for new choices.</summary>
    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }

    // Navigation
    public Workspace Workspace { get; set; } = null!;
}
