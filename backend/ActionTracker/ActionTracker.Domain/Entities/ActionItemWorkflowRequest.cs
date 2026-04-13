using System.ComponentModel.DataAnnotations.Schema;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Domain.Entities;

public class ActionItemWorkflowRequest
{
    public Guid Id { get; set; }

    public Guid ActionItemId { get; set; }

    public WorkflowRequestType RequestType { get; set; }
    public WorkflowRequestStatus Status { get; set; } = WorkflowRequestStatus.Pending;

    /// <summary>User who initiated the request (plain string, no FK).</summary>
    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedByDisplayName { get; set; } = string.Empty;

    // Date change fields (only for DateChangeRequest)
    public DateTime? RequestedNewStartDate { get; set; }
    public DateTime? RequestedNewDueDate { get; set; }

    // Status change field (only for StatusChangeRequest)
    public ActionStatus? RequestedNewStatus { get; set; }

    // Snapshots of current values at request time
    public DateTime? CurrentStartDate { get; set; }
    public DateTime? CurrentDueDate { get; set; }
    public ActionStatus? CurrentStatus { get; set; }

    /// <summary>Requester's justification.</summary>
    public string Reason { get; set; } = string.Empty;

    // Review fields (null while Pending)
    public string? ReviewedByUserId { get; set; }
    public string? ReviewedByDisplayName { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    // Navigation
    public ActionItem ActionItem { get; set; } = null!;
}
