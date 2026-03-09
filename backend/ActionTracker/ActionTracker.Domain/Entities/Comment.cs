namespace ActionTracker.Domain.Entities;

/// <summary>
/// Generic comment entity. Uses polymorphic association
/// (RelatedEntityType + RelatedEntityId) so any entity type
/// (ActionItem, Project, etc.) can own comments.
/// </summary>
public class Comment
{
    public Guid Id { get; set; }

    /// <summary>Type of the owning entity (e.g. "ActionItem", "Project").</summary>
    public string RelatedEntityType { get; set; } = string.Empty;

    /// <summary>Primary key of the owning entity.</summary>
    public Guid RelatedEntityId { get; set; }

    public string Content { get; set; } = string.Empty;
    public string AuthorUserId { get; set; } = string.Empty;
    public bool IsHighImportance { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ApplicationUser Author { get; set; } = null!;
}
