using ActionTracker.Application.Features.Notifications.DTOs;
using ActionTracker.Application.Helpers;

namespace ActionTracker.Application.Features.Notifications;

public interface INotificationService
{
    Task<NotificationDto> CreateAsync(CreateNotificationDto dto);
    Task CreateBulkAsync(List<CreateNotificationDto> dtos);
    Task<PagedResult<NotificationDto>> GetByUserAsync(string userId, int page, int pageSize, bool? isRead, string? type);
    Task<NotificationSummaryDto> GetSummaryAsync(string userId);
    Task MarkAsReadAsync(Guid notificationId, string userId);
    Task MarkAllAsReadAsync(string userId);
    Task<int> GetUnreadCountAsync(string userId);
    Task DeleteAsync(Guid notificationId, string userId);
    Task DeleteAllReadAsync(string userId);
}
