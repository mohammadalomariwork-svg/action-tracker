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
    public string AssigneeId { get; set; } = string.Empty;

    [Required]
    public ActionCategory Category { get; set; }

    [Required]
    public ActionPriority Priority { get; set; }

    public ActionStatus Status { get; set; } = ActionStatus.ToDo;

    [Required]
    public DateTime DueDate { get; set; }

    [Range(0, 100)]
    public int Progress { get; set; } = 0;

    public bool IsEscalated { get; set; } = false;

    public string Notes { get; set; } = string.Empty;
}
