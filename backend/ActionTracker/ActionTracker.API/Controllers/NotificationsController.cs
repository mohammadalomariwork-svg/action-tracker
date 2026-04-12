using System.Security.Claims;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.Notifications.DTOs;
using ActionTracker.Application.Helpers;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private string UserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isRead = null,
        [FromQuery] string? type = null)
    {
        var result = await _notificationService.GetByUserAsync(UserId, page, pageSize, isRead, type);
        return Ok(ApiResponse<PagedResult<NotificationDto>>.Ok(result));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var result = await _notificationService.GetSummaryAsync(UserId);
        return Ok(ApiResponse<NotificationSummaryDto>.Ok(result));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var count = await _notificationService.GetUnreadCountAsync(UserId);
        return Ok(ApiResponse<int>.Ok(count));
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        try
        {
            await _notificationService.MarkAsReadAsync(id, UserId);
            return Ok(ApiResponse<string>.Ok("Notification marked as read."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        await _notificationService.MarkAllAsReadAsync(UserId);
        return Ok(ApiResponse<string>.Ok("All notifications marked as read."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.NotificationsDelete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _notificationService.DeleteAsync(id, UserId);
            return Ok(ApiResponse<string>.Ok("Notification deleted."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    [HttpDelete("read")]
    [Authorize(Policy = PermissionPolicies.NotificationsDelete)]
    public async Task<IActionResult> DeleteAllRead()
    {
        await _notificationService.DeleteAllReadAsync(UserId);
        return Ok(ApiResponse<string>.Ok("All read notifications deleted."));
    }
}
