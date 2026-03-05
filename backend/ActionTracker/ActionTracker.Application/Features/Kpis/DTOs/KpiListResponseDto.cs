namespace ActionTracker.Application.Features.Kpis.DTOs;

public class KpiListResponseDto
{
    public List<KpiDto> Kpis { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
