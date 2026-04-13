# F-WF-07 — Real-Time Workflow Notifications & Toast Popups

## Context

The Angular frontend already has SignalR integration (`@microsoft/signalr`), a `NotificationBellComponent` in the header with unread count, and a `NotificationsPageComponent` that lists notifications with grouping and filtering. We need to ensure workflow notifications are handled in real-time with toast popups and correct badge updates.

## Pre-requisite

- B-WF-09 completed (backend sends SignalR push for workflow notifications)
- F-WF-06 completed (routes and notification click handling exist)
- F-WF-02 completed (WorkflowService exists for badge refresh)

## Instructions

### 1. Find the existing SignalR notification listener

Look for where the SignalR connection is established and where `ReceiveNotification` events are handled. This is likely in one of:
- `src/app/core/services/notification.service.ts`
- `src/app/core/services/signalr.service.ts`
- `src/app/layout/header/header.component.ts`
- A dedicated SignalR service

Find the handler that processes incoming real-time notifications.

### 2. Add workflow-specific toast popups

In the SignalR notification handler, when a new notification arrives, check the `actionType` and show a contextual toast message using the project's `ToastService` (ngx-toastr):

```typescript
private handleRealtimeNotification(notification: Notification): void {
  // ... existing handling ...

  // Add workflow-specific toast handling:
  switch (notification.actionType) {
    case 'DateChangeRequested':
      this.toastService.info(
        notification.message,
        'Date Change Requested',
        { 
          timeOut: 8000,
          closeButton: true,
          progressBar: true
        }
      );
      break;

    case 'DateChangeApproved':
      this.toastService.success(
        notification.message,
        'Date Change Approved ✅',
        { timeOut: 8000, closeButton: true, progressBar: true }
      );
      break;

    case 'DateChangeRejected':
      this.toastService.warning(
        notification.message,
        'Date Change Rejected',
        { timeOut: 10000, closeButton: true, progressBar: true }
      );
      break;

    case 'StatusChangeRequested':
      this.toastService.info(
        notification.message,
        'Status Change Requested',
        { timeOut: 8000, closeButton: true, progressBar: true }
      );
      break;

    case 'StatusChangeApproved':
      this.toastService.success(
        notification.message,
        'Status Change Approved ✅',
        { timeOut: 8000, closeButton: true, progressBar: true }
      );
      break;

    case 'StatusChangeRejected':
      this.toastService.warning(
        notification.message,
        'Status Change Rejected',
        { timeOut: 10000, closeButton: true, progressBar: true }
      );
      break;

    case 'ActionItemEscalated':
      this.toastService.error(
        notification.message,
        'Action Item Escalated ⚠️',
        { timeOut: 12000, closeButton: true, progressBar: true }
      );
      break;

    case 'EscalationDirectionGiven':
      this.toastService.info(
        notification.message,
        'Direction Received',
        { timeOut: 8000, closeButton: true, progressBar: true }
      );
      break;
  }
}
```

### 3. Make toasts clickable (navigate to the right page)

If ngx-toastr supports click events (via `onActivateTick` or `tapToDismiss`), add a click handler that navigates to the notification's URL:

```typescript
// When creating the toast, capture the toast reference:
const toastRef = this.toastService.info(notification.message, title, options);

// On click, navigate to the notification URL:
toastRef.onTap.subscribe(() => {
  if (notification.url) {
    this.router.navigateByUrl(notification.url);
  }
});
```

If the existing toast setup doesn't support this, check how toasts are currently configured and add the click navigation.

### 4. Refresh approval badge on workflow notifications

When a workflow notification arrives that is relevant to the approvals page, refresh the pending approval count in the header:

```typescript
// In the SignalR handler or notification service:
if (['DateChangeRequested', 'StatusChangeRequested'].includes(notification.actionType)) {
  // A new request came in — refresh the approvals badge
  this.refreshApprovalsBadge();
}

if (['DateChangeApproved', 'DateChangeRejected', 'StatusChangeApproved', 'StatusChangeRejected']
    .includes(notification.actionType)) {
  // A request was reviewed — refresh the approvals badge (count may have decreased)
  this.refreshApprovalsBadge();
}
```

The `refreshApprovalsBadge()` method should call `workflowService.getPendingSummary()` and update the count shown in the header's "My Approvals" nav link badge.

**Implementation approach:**

Create a shared signal or BehaviorSubject for the pending approvals count that both the header and the notification handler can access:

Option A: Add to the existing `NotificationService` or create a `WorkflowStateService`:
```typescript
@Injectable({ providedIn: 'root' })
export class WorkflowStateService {
  private pendingCountSubject = new BehaviorSubject<number>(0);
  pendingCount$ = this.pendingCountSubject.asObservable();

  constructor(private workflowService: WorkflowService) {}

  refreshPendingCount(): void {
    this.workflowService.getPendingSummary().subscribe(summary => {
      this.pendingCountSubject.next(summary.totalPending);
    });
  }

  clearPendingCount(): void {
    this.pendingCountSubject.next(0);
  }
}
```

Option B: If the project uses Angular signals, use a `signal<number>(0)` instead of BehaviorSubject.

The header subscribes to `pendingCount$` and displays the badge.

### 5. Refresh the notification bell count

The existing `NotificationBellComponent` shows unread notification count. When a workflow notification arrives via SignalR:
1. Increment the unread count (the bell badge)
2. If the notifications page is open, add the new notification to the top of the list

Check how the existing bell handles real-time notifications and make sure workflow notifications work the same way. This may already work if the existing SignalR handler calls the notification service's refresh method — just verify.

### 6. Update `NotificationsPageComponent` filter options

The notifications page has a filter by type: `ActionItem, Project, Milestone, Workspace, Risk, Kpi, StrategicObjective, System`. Since workflow notifications use `Type = "ActionItem"`, they'll already appear under the "ActionItem" filter. No changes needed here.

However, if the notifications page also filters by `ActionType` or shows the action type as a sub-label, add the new action types to any display maps:

```typescript
const ACTION_TYPE_LABELS: Record<string, string> = {
  // ... existing entries ...
  'DateChangeRequested': 'Date Change Requested',
  'DateChangeApproved': 'Date Change Approved',
  'DateChangeRejected': 'Date Change Rejected',
  'StatusChangeRequested': 'Status Change Requested',
  'StatusChangeApproved': 'Status Change Approved',
  'StatusChangeRejected': 'Status Change Rejected',
  'ActionItemEscalated': 'Escalated',
  'EscalationDirectionGiven': 'Direction Given',
};
```

### 7. Update the notification click handler

In the `NotificationsPageComponent` or wherever notification clicks are handled, ensure workflow notification `actionType` values route correctly. The backend sets the `Url` field, so the frontend should use `notification.url` for navigation:

```typescript
onNotificationClick(notification: Notification): void {
  // Mark as read
  this.notificationService.markAsRead(notification.id).subscribe();
  
  // Navigate using the URL from the notification
  if (notification.url) {
    this.router.navigateByUrl(notification.url);
  }
}
```

If the existing click handler uses a switch/case on `relatedEntityType` instead of `url`, add cases for the workflow action types that navigate to `/approvals` or `/actions/:id/view` as defined in B-WF-09.

### 8. Audio/visual attention for escalation notifications

Escalation is a critical event. Consider adding extra attention:
- Use `toastService.error()` (red toast) instead of `info()` for escalations
- Set a longer `timeOut` (12 seconds) so the user doesn't miss it
- If the project has a sound/vibration capability, trigger it for escalations
- The toast should be larger or have an icon to stand out

### 9. Clear workflow state on logout

In the `AuthService.logout()` method (or wherever logout cleanup happens), clear the workflow state:

```typescript
// In logout flow:
this.workflowStateService.clearPendingCount();
```

This prevents stale badge counts from persisting across sessions.

## Validation

- When a date change request is created, the creator and manager(s) see a real-time toast popup
- When a request is approved/rejected, the requester sees a real-time toast
- When an item is escalated, creator and manager see a red toast with longer duration
- When direction is given, assignees see a toast
- Clicking a toast navigates to the correct page
- The notification bell count updates in real-time
- The "My Approvals" nav badge updates when new requests arrive or are reviewed
- The notifications page shows workflow notifications with correct labels
- Clicking a notification in the notifications page navigates to the right URL
- Badge counts clear on logout
- `ng build` passes
- No console errors
