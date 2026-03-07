namespace ActionTracker.Application.Features.StrategicObjectives.DTOs;

public class StrategicObjectiveListResponseDto
{
    public List<StrategicObjectiveDto> Objectives { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
