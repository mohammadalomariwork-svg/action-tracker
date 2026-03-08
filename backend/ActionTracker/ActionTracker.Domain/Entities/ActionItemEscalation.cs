namespace ActionTracker.Domain.Entities;

public class ActionItemEscalation
{
    public Guid Id { get; set; }
    public Guid ActionItemId { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public string EscalatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ActionItem ActionItem { get; set; } = null!;
    public ApplicationUser EscalatedByUser { get; set; } = null!;
}
