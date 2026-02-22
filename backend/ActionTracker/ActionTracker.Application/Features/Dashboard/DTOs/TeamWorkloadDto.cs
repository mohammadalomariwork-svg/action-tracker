namespace ActionTracker.Application.Features.Dashboard.DTOs;

public class TeamWorkloadDto
{
    public string  UserId               { get; set; } = string.Empty;
    public string  UserName             { get; set; } = string.Empty;
    public int     AssignedCount        { get; set; }
    public int     CompletedCount       { get; set; }
    public int     OverdueCount         { get; set; }
    public decimal CompletionPercentage { get; set; }
}
