using System.ComponentModel.DataAnnotations;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.ActionItems.DTOs;

public class ActionItemUpdateDto
{
    [Required]
    public Guid Id { get; set; }

    [MaxLength(255)]
    public string? Title { get; set; }

    [MaxLength(5000)]
    public string? Description { get; set; }

    public Guid? WorkspaceId { get; set; }

    /// <summary>When supplied, replaces the full assignee list.</summary>
    public List<string>? AssigneeIds { get; set; }

    public ActionPriority? Priority { get; set; }

    public ActionStatus? Status { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? DueDate { get; set; }

    [Range(0, 100)]
    public int? Progress { get; set; }

    public bool? IsEscalated { get; set; }
}
