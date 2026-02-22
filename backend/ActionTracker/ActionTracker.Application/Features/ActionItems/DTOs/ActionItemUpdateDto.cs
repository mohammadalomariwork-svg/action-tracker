using System.ComponentModel.DataAnnotations;
using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.ActionItems.DTOs;

public class ActionItemUpdateDto
{
    [Required]
    public int Id { get; set; }

    [MaxLength(255)]
    public string? Title { get; set; }

    [MaxLength(5000)]
    public string? Description { get; set; }

    public string? AssigneeId { get; set; }

    public ActionCategory? Category { get; set; }

    public ActionPriority? Priority { get; set; }

    public ActionStatus? Status { get; set; }

    public DateTime? DueDate { get; set; }

    [Range(0, 100)]
    public int? Progress { get; set; }

    public bool? IsEscalated { get; set; }

    public string? Notes { get; set; }
}
