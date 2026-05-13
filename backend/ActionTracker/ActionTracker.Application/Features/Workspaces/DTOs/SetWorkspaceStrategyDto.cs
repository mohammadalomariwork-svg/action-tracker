namespace ActionTracker.Application.Features.Workspaces.DTOs;

/// <summary>Payload for PUT /api/workspaces/{id}/strategy.</summary>
public class SetWorkspaceStrategyDto
{
    /// <summary>
    /// The strategic plan to attach to the workspace.
    /// Pass <c>null</c> to clear the linked plan.
    /// </summary>
    public Guid? StrategicPlanId { get; set; }
}
