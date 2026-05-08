namespace ActionTracker.Application.Features.OrgChart.DTOs;

/// <summary>
/// Lightweight DTO describing the user designated as an org unit's head
/// (CR-001-H).
/// </summary>
public class OrgUnitHeadDto
{
    public Guid OrgUnitId { get; set; }
    public string OrgUnitName { get; set; } = string.Empty;
    public int OrgUnitLevel { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
