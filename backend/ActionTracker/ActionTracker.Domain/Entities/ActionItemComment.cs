namespace ActionTracker.Domain.Entities;

public class ActionItemComment
{
    public Guid Id { get; set; }
    public Guid ActionItemId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string AuthorUserId { get; set; } = string.Empty;
    public bool IsHighImportance { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ActionItem ActionItem { get; set; } = null!;
    public ApplicationUser Author { get; set; } = null!;
}
