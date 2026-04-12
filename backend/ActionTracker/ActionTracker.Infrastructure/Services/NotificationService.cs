using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.Notifications.DTOs;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IAppDbContext _db;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IAppDbContext db,
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationService> logger)
    {
        _db         = db;
        _hubContext  = hubContext;
        _logger      = logger;
    }

    public async Task<NotificationDto> CreateAsync(CreateNotificationDto dto)
    {
        var entity = new AppNotification
        {
            Id                   = Guid.NewGuid(),
            UserId               = dto.UserId,
            Title                = dto.Title,
            Message              = dto.Message,
            Type                 = dto.Type,
            ActionType           = dto.ActionType,
            RelatedEntityType    = dto.RelatedEntityType,
            RelatedEntityId      = dto.RelatedEntityId,
            RelatedEntityCode    = dto.RelatedEntityCode,
            Url                  = dto.Url,
            IsRead               = false,
            CreatedAt            = DateTime.UtcNow,
            CreatedByUserId      = dto.CreatedByUserId,
            CreatedByDisplayName = dto.CreatedByDisplayName,
        };

        _db.AppNotifications.Add(entity);
        await _db.SaveChangesAsync();

        var notificationDto = MapToDto(entity);

        // Push real-time via SignalR
        try
        {
            await _hubContext.Clients.User(dto.UserId)
                .SendAsync("ReceiveNotification", notificationDto);

            var unreadCount = await GetUnreadCountAsync(dto.UserId);
            await _hubContext.Clients.User(dto.UserId)
                .SendAsync("UnreadCountUpdated", unreadCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push SignalR notification to user {UserId}", dto.UserId);
        }

        return notificationDto;
    }

    public async Task CreateBulkAsync(List<CreateNotificationDto> dtos)
    {
        if (dtos.Count == 0) return;

        var entities = dtos.Select(dto => new AppNotification
        {
            Id                   = Guid.NewGuid(),
            UserId               = dto.UserId,
            Title                = dto.Title,
            Message              = dto.Message,
            Type                 = dto.Type,
            ActionType           = dto.ActionType,
            RelatedEntityType    = dto.RelatedEntityType,
            RelatedEntityId      = dto.RelatedEntityId,
            RelatedEntityCode    = dto.RelatedEntityCode,
            Url                  = dto.Url,
            IsRead               = false,
            CreatedAt            = DateTime.UtcNow,
            CreatedByUserId      = dto.CreatedByUserId,
            CreatedByDisplayName = dto.CreatedByDisplayName,
        }).ToList();

        _db.AppNotifications.AddRange(entities);
        await _db.SaveChangesAsync();

        // Push real-time to each affected user
        var affectedUserIds = dtos.Select(d => d.UserId).Distinct().ToList();
        foreach (var userId in affectedUserIds)
        {
            try
            {
                var userNotifications = entities
                    .Where(e => e.UserId == userId)
                    .Select(MapToDto)
                    .ToList();

                foreach (var n in userNotifications)
                {
                    await _hubContext.Clients.User(userId)
                        .SendAsync("ReceiveNotification", n);
                }

                var unreadCount = await GetUnreadCountAsync(userId);
                await _hubContext.Clients.User(userId)
                    .SendAsync("UnreadCountUpdated", unreadCount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push SignalR notifications to user {UserId}", userId);
            }
        }
    }

    public async Task<PagedResult<NotificationDto>> GetByUserAsync(
        string userId, int page, int pageSize, bool? isRead, string? type)
    {
        var query = _db.AppNotifications
            .Where(n => n.UserId == userId);

        if (isRead.HasValue)
            query = query.Where(n => n.IsRead == isRead.Value);

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(n => n.Type == type);

        var projected = query
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto
            {
                Id                   = n.Id,
                Title                = n.Title,
                Message              = n.Message,
                Type                 = n.Type,
                ActionType           = n.ActionType,
                RelatedEntityType    = n.RelatedEntityType,
                RelatedEntityId      = n.RelatedEntityId,
                RelatedEntityCode    = n.RelatedEntityCode,
                Url                  = n.Url,
                IsRead               = n.IsRead,
                ReadAt               = n.ReadAt,
                CreatedAt            = n.CreatedAt,
                CreatedByDisplayName = n.CreatedByDisplayName,
            });

        return await PagedResult<NotificationDto>.CreateAsync(projected, page, pageSize);
    }

    public async Task<NotificationSummaryDto> GetSummaryAsync(string userId)
    {
        var unreadCount = await _db.AppNotifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);

        var latest = await _db.AppNotifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(10)
            .Select(n => new NotificationDto
            {
                Id                   = n.Id,
                Title                = n.Title,
                Message              = n.Message,
                Type                 = n.Type,
                ActionType           = n.ActionType,
                RelatedEntityType    = n.RelatedEntityType,
                RelatedEntityId      = n.RelatedEntityId,
                RelatedEntityCode    = n.RelatedEntityCode,
                Url                  = n.Url,
                IsRead               = n.IsRead,
                ReadAt               = n.ReadAt,
                CreatedAt            = n.CreatedAt,
                CreatedByDisplayName = n.CreatedByDisplayName,
            })
            .ToListAsync();

        return new NotificationSummaryDto
        {
            UnreadCount         = unreadCount,
            LatestNotifications = latest,
        };
    }

    public async Task MarkAsReadAsync(Guid notificationId, string userId)
    {
        var notification = await _db.AppNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId)
            ?? throw new KeyNotFoundException($"Notification {notificationId} not found.");

        if (notification.IsRead) return;

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Push updated unread count
        try
        {
            var unreadCount = await GetUnreadCountAsync(userId);
            await _hubContext.Clients.User(userId)
                .SendAsync("UnreadCountUpdated", unreadCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push unread count update to user {UserId}", userId);
        }
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        var unread = await _db.AppNotifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        if (unread.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = now;
        }

        await _db.SaveChangesAsync();

        // Push unread count = 0
        try
        {
            await _hubContext.Clients.User(userId)
                .SendAsync("UnreadCountUpdated", 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push unread count update to user {UserId}", userId);
        }
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _db.AppNotifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task DeleteAsync(Guid notificationId, string userId)
    {
        var notification = await _db.AppNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId)
            ?? throw new KeyNotFoundException($"Notification {notificationId} not found.");

        _db.AppNotifications.Remove(notification);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAllReadAsync(string userId)
    {
        var read = await _db.AppNotifications
            .Where(n => n.UserId == userId && n.IsRead)
            .ToListAsync();

        if (read.Count == 0) return;

        _db.AppNotifications.RemoveRange(read);
        await _db.SaveChangesAsync();
    }

    private static NotificationDto MapToDto(AppNotification n) => new()
    {
        Id                   = n.Id,
        Title                = n.Title,
        Message              = n.Message,
        Type                 = n.Type,
        ActionType           = n.ActionType,
        RelatedEntityType    = n.RelatedEntityType,
        RelatedEntityId      = n.RelatedEntityId,
        RelatedEntityCode    = n.RelatedEntityCode,
        Url                  = n.Url,
        IsRead               = n.IsRead,
        ReadAt               = n.ReadAt,
        CreatedAt            = n.CreatedAt,
        CreatedByDisplayName = n.CreatedByDisplayName,
    };
}
