namespace ActionTracker.Domain.Entities;

public class ProjectSponsor
{
    public Guid ProjectId { get; set; }
    public string UserId { get; set; } = string.Empty;

    // Navigation
    public Project Project { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
