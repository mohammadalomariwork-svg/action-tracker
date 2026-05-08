namespace ActionTracker.Application.Features.StrategicPlans.DTOs;

public class StrategicPlanListResponseDto
{
    public List<StrategicPlanDto> Plans { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
