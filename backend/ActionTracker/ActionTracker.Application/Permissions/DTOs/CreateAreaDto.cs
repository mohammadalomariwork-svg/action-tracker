using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Permissions.DTOs;

public class CreateAreaDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public int DisplayOrder { get; set; }
}
