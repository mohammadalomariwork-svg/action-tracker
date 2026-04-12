namespace ActionTracker.Domain.Entities;

public class EmailLog
{
    public Guid Id { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? SentByUserId { get; set; }
}
