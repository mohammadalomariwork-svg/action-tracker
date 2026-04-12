namespace ActionTracker.Application.Features.Notifications.DTOs;

public class CreateNotificationDto
{
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityCode { get; set; }
    public string? Url { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? CreatedByDisplayName { get; set; }
}
