using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.ActionItems.DTOs;

public class ActionItemCommentResponseDto
{
    public Guid Id { get; set; }
    public Guid ActionItemId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string AuthorUserId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public bool IsHighImportance { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateCommentDto
{
    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    public bool IsHighImportance { get; set; }
}

public class UpdateCommentDto
{
    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    public bool IsHighImportance { get; set; }
}
