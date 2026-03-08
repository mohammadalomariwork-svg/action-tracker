namespace ActionTracker.Application.Features.Dashboard.DTOs;

public class RecentActivityDto
{
    public Guid     Id           { get; set; }
    public string   ActionId     { get; set; } = string.Empty;
    public string   Title        { get; set; } = string.Empty;
    public string   AssigneeName { get; set; } = string.Empty;
    public DateTime CreatedAt    { get; set; }
    public string   Status       { get; set; } = string.Empty;
}
