namespace ActionTracker.Domain.Entities;

/// <summary>
/// Links an action item to one of its workspace's tags (WO-2026-11 A-5).
/// The tag always belongs to the item's workspace; the service keeps it so
/// when the item moves (tags are carried over by name).
/// </summary>
public class ActionItemTag
{
    public Guid ActionItemId { get; set; }
    public Guid TagId { get; set; }

    // Navigation properties
    public ActionItem ActionItem { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
