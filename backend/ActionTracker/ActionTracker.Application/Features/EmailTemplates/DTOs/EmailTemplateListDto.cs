namespace ActionTracker.Application.Features.EmailTemplates.DTOs;

public class EmailTemplateListDto
{
    public Guid Id { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? Description { get; set; }
}
