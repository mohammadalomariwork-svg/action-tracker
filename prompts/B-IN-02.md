# B-IN-02 — NotificationService + SignalR NotificationHub

## Context
B-IN-01 created the `AppNotification` entity. This prompt builds the service layer, SignalR hub for real-time push, and the API controller.

## Requirements

### 1. Install NuGet package (if not already present)
`Microsoft.AspNetCore.SignalR` should already be available in ASP.NET Core 9. No additional package needed.

### 2. Create DTOs in `ActionTracker.Application/Features/Notifications/DTOs/`

**NotificationDto** (response):
- Id (Guid), Title, Message, Type, ActionType
- RelatedEntityType, RelatedEntityId, RelatedEntityCode, Url
- IsRead, ReadAt, CreatedAt, CreatedByDisplayName

**NotificationSummaryDto** (for header badge):
- UnreadCount (int), LatestNotifications (List<NotificationDto> — last 5 unread)

**CreateNotificationDto** (internal — not exposed via API):
- UserId (string), Title, Message, Type, ActionType
- RelatedEntityType, RelatedEntityId, RelatedEntityCode, Url
- CreatedByUserId, CreatedByDisplayName

### 3. Create `INotificationService` in `ActionTracker.Application/Features/Notifications/`

```
Task<NotificationDto> CreateAsync(CreateNotificationDto dto);
Task CreateBulkAsync(List<CreateNotificationDto> dtos);
Task<PagedResult<NotificationDto>> GetByUserAsync(string userId, int page, int pageSize, bool? isRead, string? type);
Task<NotificationSummaryDto> GetSummaryAsync(string userId);
Task MarkAsReadAsync(Guid notificationId, string userId);
Task MarkAllAsReadAsync(string userId);
Task<int> GetUnreadCountAsync(string userId);
Task DeleteAsync(Guid notificationId, string userId);
Task DeleteAllReadAsync(string userId);
```

### 4. Create `NotificationService` in `ActionTracker.Infrastructure/Services/`

Implements `INotificationService`:

**CreateAsync:**
1. Create `AppNotification` entity from DTO, save to DB
2. Map to `NotificationDto`
3. Push to SignalR: call `IHubContext<NotificationHub>.Clients.User(dto.UserId).SendAsync("ReceiveNotification", notificationDto)`
4. Also push updated unread count: `SendAsync("UnreadCountUpdated", newCount)`
5. Return the created DTO

**CreateBulkAsync:**
- Insert all notifications in one `SaveChangesAsync` call
- Push each notification to its respective user via SignalR
- Push updated unread count to each affected user

**GetByUserAsync:**
- Paginated query filtered by userId, optional isRead filter, optional type filter
- Ordered by CreatedAt descending

**GetSummaryAsync:**
- Count unread where UserId matches
- Fetch last 5 unread notifications

**MarkAsReadAsync:**
- Set `IsRead = true`, `ReadAt = DateTime.UtcNow`
- Push updated unread count via SignalR

**MarkAllAsReadAsync:**
- Bulk update all unread for user
- Push unread count = 0 via SignalR

### 5. Create `NotificationHub` in `ActionTracker.Api/Hubs/`

```csharp
[Authorize(AuthenticationSchemes = "MultiAuth")]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // User automatically gets their own group via User.Identity
        await base.OnConnectedAsync();
    }
}
```

SignalR uses the `NameIdentifier` claim as the user identifier. Since we use dual auth, configure `IUserIdProvider` if needed to extract user ID from the JWT.

### 6. Create `NotificationsController` in `ActionTracker.Api/Controllers/`

Route: `api/notifications`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/` | Authenticated | Paginated notifications for current user. Query: page, pageSize, isRead, type |
| GET | `/summary` | Authenticated | Unread count + latest 5 unread |
| GET | `/unread-count` | Authenticated | Just the unread count (int) |
| PATCH | `/{id}/read` | Authenticated | Mark single notification as read |
| POST | `/mark-all-read` | Authenticated | Mark all as read for current user |
| DELETE | `/{id}` | Authenticated | Delete single notification |
| DELETE | `/read` | Authenticated | Delete all read notifications for current user |

All endpoints scope to current user — users can only access their own notifications.

### 7. Configure SignalR in startup

Add to `Program.cs` / startup:
- `builder.Services.AddSignalR();`
- `app.MapHub<NotificationHub>("/hubs/notifications");`
- Ensure CORS allows the Angular dev server origin for SignalR WebSocket connections
- Add SignalR to the CORS policy: `.AllowCredentials()` (required for SignalR)

### 8. Register services in DI
- `INotificationService` → `NotificationService`

## Rules
- All async
- No FK to AspNetUsers
- SignalR hub requires authentication (same MultiAuth scheme)
- Notifications are user-scoped — no cross-user access
- Push real-time via SignalR on create and read status changes
- UTC for all DateTime fields
