using System.ComponentModel.DataAnnotations;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.ActionItems.DTOs;

public class ActionItemCreateDto
{
    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public Guid WorkspaceId { get; set; }

    /// <summary>At least one assignee is required.</summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one assignee is required.")]
    public List<string> AssigneeIds { get; set; } = new();

    [Required]
    public ActionPriority Priority { get; set; }

    public ActionStatus Status { get; set; } = ActionStatus.ToDo;

    public DateTime? StartDate { get; set; }

    [Required]
    public DateTime DueDate { get; set; }

    [Range(0, 100)]
    public int Progress { get; set; } = 0;

    public bool IsEscalated { get; set; } = false;
}
