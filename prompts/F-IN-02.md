# F-IN-02 — Notification Bell + Dropdown in Header

## Context
F-IN-01 created the notification models, service, and SignalR connection. This prompt builds the notification bell icon with unread badge and a dropdown panel in the header component.

## Requirements

### 1. Create `NotificationBellComponent` in `src/app/shared/components/notification-bell/`

Standalone component. Embedded inside the existing `HeaderComponent`.

**Layout:**
- Bell icon using Bootstrap Icons (`bi-bell` / `bi-bell-fill`)
- Unread count badge: red circular badge overlaying the bell icon top-right corner, showing the number (e.g., "3"). Hide when count is 0. Show "9+" when count exceeds 9.
- On click: toggles a dropdown panel below the bell

**Dropdown panel:**
- Fixed width: 380px, max-height: 480px with overflow-y scroll
- Header row: "Notifications" title on the left, "Mark all as read" link on the right
- List of latest notifications (up to 10 items from `latestNotifications$` + load more from API)
- Each notification item:
  - Icon on the left based on `type`: ActionItem = `bi-check2-square`, Project = `bi-folder`, Milestone = `bi-flag`, Workspace = `bi-building`, Risk = `bi-exclamation-triangle`, Kpi = `bi-graph-up`, StrategicObjective = `bi-bullseye`, System = `bi-info-circle`
  - Icon color based on `actionType`: Created = blue, Completed = green, Overdue = red, Escalated = orange, StatusChanged = purple, Assigned = teal
  - Title (bold if unread, normal weight if read)
  - Message (truncated to 80 chars)
  - Relative time (e.g., "2 minutes ago", "1 hour ago", "Yesterday") — use `date-fns` `formatDistanceToNow`
  - Unread indicator: blue dot on the left edge for unread items
  - On click: mark as read, navigate to `notification.url` (use Angular Router), close dropdown
- Empty state: "No notifications" with a muted bell icon
- Footer: "View all notifications" link → navigates to `/notifications`

**Behavior:**
- Subscribe to `notificationService.unreadCount$` for badge count
- Subscribe to `notificationService.newNotification$` for real-time updates — when a new notification arrives:
  - Prepend to the dropdown list
  - Briefly animate the bell icon (CSS shake/bounce animation, 1 second)
  - Play no sound (keep it non-intrusive)
- On "Mark all as read": call `notificationService.markAllAsRead()`, update all items in list to read state
- On individual item click: call `notificationService.markAsRead(id)`, navigate to URL
- Close dropdown when clicking outside (use `@HostListener` or Angular CDK overlay)
- Close dropdown on route navigation

### 2. Integrate into `HeaderComponent`

Modify the existing `HeaderComponent` (this modifies an existing file):
- Add `<app-notification-bell>` next to the user profile/avatar area in the header, positioned to the left of the user dropdown
- Only show when user is authenticated

### 3. Styling (SCSS)

```scss
// Notification bell specific styles
.notification-bell {
  position: relative;
  cursor: pointer;
  
  .badge-count {
    position: absolute;
    top: -4px;
    right: -4px;
    min-width: 18px;
    height: 18px;
    font-size: 0.65rem;
    // Use Bootstrap danger color
  }
}

.notification-dropdown {
  position: absolute;
  right: 0;
  top: 100%;
  width: 380px;
  max-height: 480px;
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.12);
  z-index: 1050;
  overflow: hidden;
  
  .notification-item {
    padding: 12px 16px;
    border-bottom: 1px solid #f0f0f0;
    cursor: pointer;
    transition: background-color 0.15s;
    
    &:hover {
      background-color: #f8f9fa;
    }
    
    &.unread {
      background-color: #f0f4ff;
    }
  }
}

// Bell shake animation for new notifications
@keyframes bell-shake {
  0%, 100% { transform: rotate(0); }
  15% { transform: rotate(15deg); }
  30% { transform: rotate(-15deg); }
  45% { transform: rotate(10deg); }
  60% { transform: rotate(-10deg); }
  75% { transform: rotate(5deg); }
}

.bell-animate {
  animation: bell-shake 0.6s ease-in-out;
}
```

## Rules
- Standalone component
- Bootstrap 5 + Bootstrap Icons
- SCSS with variables, white/light surfaces only
- Sapphire blue `#0F52BA` accent color
- date-fns for relative time formatting
- Responsive: on mobile (< 576px), dropdown becomes full-width
- No `any` types
- Close dropdown on outside click and route change
- Do NOT use Angular Material overlays — use pure Bootstrap/CSS positioning
