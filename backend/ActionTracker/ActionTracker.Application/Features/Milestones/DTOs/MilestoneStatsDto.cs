namespace ActionTracker.Application.Features.Milestones.DTOs;

public class MilestoneStatsDto
{
    public int TotalActionItems { get; set; }
    public decimal CompletionRate { get; set; }
    public decimal OnTimeDeliveryRate { get; set; }
    public int EscalatedActionItems { get; set; }
}
