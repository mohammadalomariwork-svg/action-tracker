namespace ActionTracker.Domain.Entities;

/// <summary>
/// Junction table linking a sub-item to its assigned users (WO-2026-11 A-2),
/// mirroring <see cref="ActionItemAssignee"/>. Being assigned to a sub-item
/// does not make a user an assignee of the parent action item.
/// </summary>
public class ActionItemSubItemAssignee
{
    public Guid SubItemId { get; set; }
    public string UserId { get; set; } = string.Empty;

    // Navigation properties
    public ActionItemSubItem SubItem { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
