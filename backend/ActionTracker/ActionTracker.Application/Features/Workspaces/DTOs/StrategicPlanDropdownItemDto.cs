namespace ActionTracker.Application.Features.Workspaces.DTOs;

/// <summary>Lightweight strategic-plan item returned by GET /api/workspaces/strategic-plans.</summary>
public class StrategicPlanDropdownItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid OrgUnitId { get; set; }
    public string OrgUnitName { get; set; } = string.Empty;
    public int StartYear { get; set; }
    public int EndYear { get; set; }
    public string Status { get; set; } = string.Empty;
}
