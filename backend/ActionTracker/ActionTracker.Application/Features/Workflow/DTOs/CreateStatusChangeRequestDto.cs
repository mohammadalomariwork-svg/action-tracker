using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.Workflow.DTOs;

public class CreateStatusChangeRequestDto
{
    public Guid ActionItemId { get; set; }
    public ActionStatus NewStatus { get; set; }
    public string Reason { get; set; } = string.Empty;
}
