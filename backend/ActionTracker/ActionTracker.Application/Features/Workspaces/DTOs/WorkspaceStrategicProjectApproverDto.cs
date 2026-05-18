using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Workspaces.DTOs;

/// <summary>
/// One entry in a workspace's sequential strategic-project approver chain.
/// Returned by <c>GET /api/workspaces/{id}/strategic-project-approvers</c>.
/// </summary>
public class WorkspaceStrategicProjectApproverDto
{
    public string UserId      { get; set; } = string.Empty;
    public string UserName    { get; set; } = string.Empty;
    public string Email       { get; set; } = string.Empty;
    public string OrgUnitName { get; set; } = string.Empty;
    /// <summary>1-based position in the chain.</summary>
    public int    Order       { get; set; }
    public DateTime AddedAt   { get; set; }
}

/// <summary>Payload for POST /api/workspaces/{id}/strategic-project-approvers.</summary>
public class AddWorkspaceStrategicProjectApproverDto
{
    public string UserId   { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}

/// <summary>
/// Payload for PUT /api/workspaces/{id}/strategic-project-approvers/reorder —
/// replaces the entire chain with the given user-id list, applying 1-based
/// Order in the array's iteration order.
/// </summary>
public class ReorderWorkspaceStrategicProjectApproversDto
{
    [Required]
    public List<string> UserIds { get; set; } = new();
}
