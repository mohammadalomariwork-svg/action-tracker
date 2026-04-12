using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.EmailTemplates.DTOs;

public class UpdateEmailTemplateDto
{
    [Required]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string HtmlBody { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
