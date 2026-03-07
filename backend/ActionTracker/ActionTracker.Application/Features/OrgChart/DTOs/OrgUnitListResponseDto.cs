namespace ActionTracker.Application.Features.OrgChart.DTOs;

public class OrgUnitListResponseDto
{
    public List<OrgUnitDto> OrgUnits { get; set; } = new();
    public int TotalCount { get; set; }
}
