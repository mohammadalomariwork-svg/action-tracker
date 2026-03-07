namespace ActionTracker.Application.Features.Dashboard.DTOs;

public class AtRiskItemDto
{
    public int      Id            { get; set; }
    public string   ActionId      { get; set; } = string.Empty;
    public string   Title         { get; set; } = string.Empty;
    public string   AssigneeName  { get; set; } = string.Empty;
    public string   Priority      { get; set; } = string.Empty;
    public string   Status        { get; set; } = string.Empty;
    public DateTime DueDate       { get; set; }
    public int      DaysOverdue   { get; set; }
    public bool     IsEscalated   { get; set; }

    /// <summary>"Critical", "High", or "Medium"</summary>
    public string   SeverityLevel { get; set; } = string.Empty;
}
