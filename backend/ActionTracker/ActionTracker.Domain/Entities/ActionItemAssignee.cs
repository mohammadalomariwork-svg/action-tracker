namespace ActionTracker.Domain.Entities;

/// <summary>
/// Junction table linking ActionItems to their assigned users (many-to-many).
/// </summary>
public class ActionItemAssignee
{
    public Guid ActionItemId { get; set; }
    public string UserId { get; set; } = string.Empty;

    // Navigation properties
    public ActionItem ActionItem { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
