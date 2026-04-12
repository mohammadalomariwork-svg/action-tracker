# F-IN-03 — Notifications Page (Full List, Mark-Read, Filters)

## Context
F-IN-02 created the notification bell dropdown in the header. This prompt builds a dedicated full-page notifications view where users can browse, filter, and manage all their notifications.

## Requirements

### 1. Add route
In the existing app routing configuration, add:
- `/notifications` → `NotificationsPageComponent` (requires `authGuard`)

This modifies the existing routing file.

### 2. Create `NotificationsPageComponent` in `src/app/features/notifications/notifications-page/`

Standalone component.

**Layout:**

**Page header:**
- Title: "Notifications"
- Action buttons row:
  - "Mark all as read" button (outline primary) — disabled when unread count is 0
  - "Clear read" button (outline secondary) — deletes all read notifications, with confirm dialog
  - Unread count display: "X unread"

**Filters row:**
- Type filter dropdown: All, ActionItem, Project, Milestone, Workspace, Risk, Kpi, StrategicObjective, System
- Read status filter: All, Unread Only, Read Only
- Filters apply immediately on change (no apply button)

**Notification list:**
- Full-width card-based list (not a table — cards look better for notifications)
- Each notification card:
  - Left: colored icon based on `type` (same icon mapping as F-IN-02)
  - Center:
    - Title (bold if unread)
    - Message (full text, not truncated)
    - Meta line: `createdByDisplayName` · relative time (date-fns `formatDistanceToNow`) · entity code badge (e.g., "ACT-001")
  - Right:
    - Unread blue dot indicator
    - Dropdown menu (three-dot icon): "Mark as read" / "Mark as unread", "Delete"
  - Click on the card body: mark as read + navigate to `notification.url`
- Group notifications by date: "Today", "Yesterday", "This Week", "Earlier"
- Pagination: "Load more" button at the bottom (append pattern, not page replacement)
  - Load 20 notifications per page
  - Show "Loading..." spinner while fetching
  - Show "No more notifications" when all loaded

**Empty state:**
- Large muted bell icon
- "You're all caught up!"
- "No notifications to show" subtitle

**Behavior:**
- On init: load first page of notifications with current filters
- Filter changes: reset list, reload from page 1
- "Mark all as read": call API, update all items in current list to read state, reset unread count
- "Clear read": show confirm dialog, call `deleteAllRead()`, remove read items from list
- Individual mark as read: call API, update item state, decrement unread count
- Individual delete: call API, remove from list with fade-out animation
- Real-time: subscribe to `newNotification$` — prepend new notifications to the top of the list with a slide-in animation (only if filter allows — e.g., if filtering "Read Only", don't show new unread)

### 3. Add "View all notifications" navigation
This is already handled in F-IN-02 (footer link in the dropdown). Ensure the route works.

### 4. Styling (SCSS)

```scss
.notification-page {
  max-width: 800px;
  margin: 0 auto;
  
  .notification-card {
    background: #fff;
    border: 1px solid #e9ecef;
    border-radius: 8px;
    padding: 16px;
    margin-bottom: 8px;
    cursor: pointer;
    transition: all 0.2s;
    
    &:hover {
      border-color: #0F52BA;
      box-shadow: 0 2px 8px rgba(15, 82, 186, 0.08);
    }
    
    &.unread {
      background-color: #f8faff;
      border-left: 3px solid #0F52BA;
    }
    
    &.read {
      opacity: 0.75;
    }
  }
  
  .date-group-header {
    font-size: 0.8rem;
    font-weight: 600;
    color: #6c757d;
    text-transform: uppercase;
    letter-spacing: 0.5px;
    padding: 12px 0 8px;
    border-bottom: 1px solid #e9ecef;
    margin-bottom: 8px;
  }
}

// Animations
.notification-enter {
  animation: slideDown 0.3s ease-out;
}

.notification-exit {
  animation: fadeOut 0.2s ease-out forwards;
}

@keyframes slideDown {
  from { opacity: 0; transform: translateY(-10px); }
  to { opacity: 1; transform: translateY(0); }
}

@keyframes fadeOut {
  from { opacity: 1; }
  to { opacity: 0; height: 0; padding: 0; margin: 0; overflow: hidden; }
}
```

### 5. Add navigation link to header

In the existing `HeaderComponent` navigation menu, add a "Notifications" link visible to all authenticated users. This modifies the existing component. The bell icon in the header already links to this page via "View all notifications", but there should also be a nav item in the menu for discoverability.

## Rules
- Standalone component
- Bootstrap 5 + Bootstrap Icons
- SCSS with variables, white/light surfaces only
- date-fns for relative time formatting and date grouping
- Responsive: cards stack full-width on mobile
- No `any` types
- Use `ConfirmDialogComponent` for destructive actions
- ngx-toastr for success/error messages
- Use "Load more" pagination (not page-based)
- Subscribe to SignalR for real-time updates on this page
