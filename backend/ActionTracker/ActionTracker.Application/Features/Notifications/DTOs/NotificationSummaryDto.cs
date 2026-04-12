namespace ActionTracker.Application.Features.Notifications.DTOs;

public class NotificationSummaryDto
{
    public int UnreadCount { get; set; }
    public List<NotificationDto> LatestNotifications { get; set; } = [];
}
