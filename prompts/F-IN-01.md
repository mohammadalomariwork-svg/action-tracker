# F-IN-01 — Notification Models, Service, SignalR Connection

## Context
The backend exposes `api/notifications` endpoints and a SignalR hub at `/hubs/notifications`. This prompt sets up the Angular frontend: models, API service, and real-time SignalR connection.

## Requirements

### 1. Install SignalR client package
```bash
npm install @microsoft/signalr
```

### 2. Create models in `src/app/models/notification.model.ts`

```typescript
export interface AppNotification {
  id: string;
  title: string;
  message: string;
  type: string; // ActionItem, Project, Milestone, Workspace, Risk, Kpi, StrategicObjective, System
  actionType: string; // Created, Updated, StatusChanged, Completed, Assigned, Escalated, Overdue, Deleted
  relatedEntityType?: string;
  relatedEntityId?: string;
  relatedEntityCode?: string;
  url?: string;
  isRead: boolean;
  readAt?: string;
  createdAt: string;
  createdByDisplayName?: string;
}

export interface NotificationSummary {
  unreadCount: number;
  latestNotifications: AppNotification[];
}
```

### 3. Create `NotificationService` in `src/app/services/notification.service.ts`

This service manages both REST API calls and the SignalR real-time connection.

**Properties (BehaviorSubjects):**
- `unreadCount$: BehaviorSubject<number>` (initial 0)
- `latestNotifications$: BehaviorSubject<AppNotification[]>` (initial [])
- `newNotification$: Subject<AppNotification>` (emits each real-time notification)

**REST API Methods:**
- `getNotifications(page: number, pageSize: number, isRead?: boolean, type?: string): Observable<PagedResult<AppNotification>>`
- `getSummary(): Observable<ApiResponse<NotificationSummary>>`
- `getUnreadCount(): Observable<ApiResponse<number>>`
- `markAsRead(id: string): Observable<ApiResponse<void>>`
- `markAllAsRead(): Observable<ApiResponse<void>>`
- `deleteNotification(id: string): Observable<ApiResponse<void>>`
- `deleteAllRead(): Observable<ApiResponse<void>>`

**SignalR Methods:**

`startConnection(): void`
1. Build HubConnection using `HubConnectionBuilder`
   - URL: `${environment.apiUrl.replace('/api', '')}/hubs/notifications`
   - Access token factory: `() => localStorage.getItem('access_token') || ''`
   - Automatic reconnect with retry intervals: [0, 2000, 5000, 10000, 30000]
2. Register handlers:
   - `ReceiveNotification`: receives `AppNotification`, push to `newNotification$`, prepend to `latestNotifications$` (keep max 5), increment `unreadCount$`
   - `UnreadCountUpdated`: receives number, update `unreadCount$`
3. Start connection, log errors

`stopConnection(): void`
- Stop the hub connection

**Lifecycle:**
- Call `startConnection()` after login (when token is available)
- Call `stopConnection()` on logout
- On connection start, immediately call `getSummary()` to load initial state
- Handle reconnection: on reconnect, reload summary

### 4. Initialize NotificationService on login

Modify `AuthService` (existing file):
- After successful login (both local and Azure AD), call `notificationService.startConnection()`
- On logout, call `notificationService.stopConnection()`

### 5. Load initial notification state in APP_INITIALIZER

In the existing APP_INITIALIZER (where permissions are loaded), also load the notification summary if user is authenticated. This initializes `unreadCount$` and `latestNotifications$` on page refresh.

## Rules
- Strongly typed — no `any`
- Use existing `ApiResponse<T>` and `PagedResult<T>` wrappers
- SignalR connection uses JWT token from localStorage
- Service is `providedIn: 'root'` (singleton)
- Handle connection errors gracefully — log and retry, never crash
- BehaviorSubjects for reactive state management
