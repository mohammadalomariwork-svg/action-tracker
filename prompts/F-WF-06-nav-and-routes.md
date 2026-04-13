# F-WF-06 — Routes, Navigation, and Approval Badge

## Context

The Angular app uses `app.routes.ts` for route definitions, `HeaderComponent` for navigation with permission-based visibility, and guards for route protection. We need to add the My Approvals page to the routing and navigation, and show a badge with the pending approvals count.

## Pre-requisite

- F-WF-03 completed (MyApprovalsComponent exists)
- F-WF-05 completed (shared review dialog exists)

## Instructions

### 1. Add route to `app.routes.ts`

**File:** `src/app/app.routes.ts` (exists)

Add the My Approvals route alongside the existing action item routes:

```typescript
{
  path: 'approvals',
  loadComponent: () => import('./features/workflow/my-approvals/my-approvals.component')
    .then(m => m.MyApprovalsComponent),
  canActivate: [authGuard],
  data: { title: 'My Approvals' }
}
```

Note: No specific permission guard needed — any authenticated user can view their own approvals. The data is already scoped by the backend to only return requests relevant to the user.

### 2. Update `HeaderComponent` navigation

**File:** Find the existing header component (likely `src/app/layout/header/header.component.ts` and `.html`)

Add a "My Approvals" link to the navigation. Place it near "My Actions" and "My Projects" since it's a personal page:

```
| My Actions | My Projects | My Approvals | ...
```

**Visibility:** Show to all authenticated users (no permission check needed).

#### Approval Count Badge

Similar to the notification bell icon's unread count, add a badge to the "My Approvals" nav link showing the pending count:

1. In the header component, inject `WorkflowService`
2. On init (and on interval or SignalR event), call `workflowService.getPendingSummary()`
3. If `totalPending > 0`, show a Bootstrap badge next to the nav link text:

```html
<a routerLink="/approvals" routerLinkActive="active" class="nav-link">
  <i class="bi bi-clipboard-check"></i>
  My Approvals
  <span *ngIf="pendingApprovalCount > 0" 
        class="badge rounded-pill bg-danger ms-1">
    {{ pendingApprovalCount }}
  </span>
</a>
```

#### Refresh strategy:
- Load on header init
- Refresh every 60 seconds (use `interval` from rxjs, unsubscribe on destroy)
- Also refresh when the user navigates to the approvals page (or use a shared signal/subject)
- If the project has SignalR integration for notifications, also listen for workflow notification events to trigger a refresh

### 3. Mobile Navigation

The header has a mobile hamburger drawer. Ensure "My Approvals" also appears there with the same badge. Check the existing mobile drawer template and add the link in the same section as "My Actions".

### 4. Update browser title

If the app uses a title strategy (resolving from route data), the My Approvals page should show: "My Approvals — KU Action Tracker"

### 5. Add deep link support

The email templates from B-WF-03 include `{{ApprovalsUrl}}` and `{{ActionUrl}}` placeholders. Ensure these routes work for deep linking:

- `/approvals` — My Approvals page (already added above)
- `/actions/:id/view` — Action item detail (already exists)

The backend service fills these URLs using `App:FrontendBaseUrl` from config. No frontend changes needed for this — just verify the routes exist.

### 6. Notification click handling

In the existing `NotificationsPageComponent` or notification click handler, add handling for workflow notification types:

When a notification has:
- `actionType` = `DateChangeRequested` or `StatusChangeRequested` → navigate to `/approvals`
- `actionType` = `DateChangeReviewed` or `StatusChangeReviewed` → navigate to `/actions/{relatedEntityId}/view`
- `actionType` = `Escalated` → navigate to `/actions/{relatedEntityId}/view`
- `actionType` = `DirectionGiven` → navigate to `/actions/{relatedEntityId}/view`

Check how existing notification click handlers work (likely using `relatedEntityType` and `relatedEntityId` to construct a route) and add the new cases.

### 7. Update ActionStatus display utilities

Anywhere in the app that displays action item statuses (status badges, filters, dropdowns), ensure the two new statuses are handled:

- **Deferred**: badge class `bg-secondary`, label "Deferred", icon `bi-pause-circle` 
- **Cancelled**: badge class `bg-dark`, label "Cancelled", icon `bi-x-circle`

Check these files/components:
- `StatusBadgeComponent` — add the new statuses
- Any status filter dropdowns in action list, reports, etc.
- Dashboard charts that group by status

## Validation

- `/approvals` route works and shows the My Approvals page
- Header shows "My Approvals" link with badge count
- Badge updates when pending requests change
- Mobile navigation includes the link
- Notification clicks navigate to the correct pages
- New statuses (Deferred, Cancelled) display correctly throughout the app
- `ng build` passes
- No console errors
