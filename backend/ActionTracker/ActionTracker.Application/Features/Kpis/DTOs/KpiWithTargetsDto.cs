namespace ActionTracker.Application.Features.Kpis.DTOs;

public class KpiWithTargetsDto : KpiDto
{
    public List<KpiTargetDto> Targets { get; set; } = new();
}
