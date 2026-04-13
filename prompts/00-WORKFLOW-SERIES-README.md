# Action Item Workflow — Prompt Series

> **Feature:** Standalone Action Item Workflow (Date Freeze, Escalation Notifications, Status Change Approval)
> **Date:** 2026-04-12
> **Prefix:** B-WF (Backend), F-WF (Frontend)
> **Total Prompts:** 16 (9 backend + 7 frontend)

---

## Execution Order (Strict — Do Not Skip)

| #  | File                              | Layer    | Depends On     | Description                                              |
|----|-----------------------------------|----------|----------------|----------------------------------------------------------|
| 1  | `B-WF-01-domain-entities.md`      | Backend  | —              | New domain entities & enums for workflow                 |
| 2  | `B-WF-02-migration.md`            | Backend  | B-WF-01        | EF Core migration for new tables                         |
| 3  | `B-WF-03-email-templates.md`      | Backend  | B-WF-02        | Seed 6 new email templates for workflow events           |
| 4  | `B-WF-04-dtos.md`                 | Backend  | B-WF-01        | DTOs for workflow requests and responses                 |
| 5  | `B-WF-05-service.md`              | Backend  | B-WF-04        | IActionItemWorkflowService interface + impl              |
| 6  | `B-WF-06-controller.md`           | Backend  | B-WF-05        | WorkflowController endpoints                             |
| 7  | `B-WF-07-permissions.md`          | Backend  | B-WF-06        | Permission catalog seeder update                         |
| 8  | `B-WF-08-hooks.md`                | Backend  | B-WF-05        | Hook workflow into existing ActionItemService            |
| 9  | `B-WF-09-notifications.md`        | Backend  | B-WF-05        | IWorkflowNotificationHelper — in-app notifications, SignalR push, email dispatch, manager resolution, deduplication |
| 10 | `F-WF-01-models.md`               | Frontend | B-WF-04        | TypeScript interfaces for workflow                       |
| 11 | `F-WF-02-service.md`              | Frontend | F-WF-01        | Angular WorkflowService for API calls                    |
| 12 | `F-WF-03-approvals-page.md`       | Frontend | F-WF-02        | My Approvals page (pending requests list)                |
| 13 | `F-WF-04-action-form-freeze.md`   | Frontend | F-WF-02        | Update ActionForm to respect date freeze                 |
| 14 | `F-WF-05-status-change-ui.md`     | Frontend | F-WF-02        | Status change request dialog + escalation direction in action detail |
| 15 | `F-WF-06-nav-and-routes.md`       | Frontend | F-WF-03        | Add routes, nav links, notification click handling       |
| 16 | `F-WF-07-realtime-notifications.md`| Frontend | F-WF-06, B-WF-09 | SignalR toast popups, clickable toasts, approval badge refresh, WorkflowStateService, logout cleanup |

---

## Dependency Graph

```
B-WF-01 (Entities & Enums)
  ├── B-WF-02 (Migration)
  │     └── B-WF-03 (Email Templates Seed)
  ├── B-WF-04 (DTOs)
  │     └── B-WF-05 (Workflow Service)
  │           ├── B-WF-06 (Controller)
  │           │     └── B-WF-07 (Permissions)
  │           ├── B-WF-08 (Hooks into ActionItemService)
  │           └── B-WF-09 (Notification Helper + SignalR Push)
  └── F-WF-01 (TS Models)
        └── F-WF-02 (Angular Service)
              ├── F-WF-03 (My Approvals Page)
              │     └── F-WF-06 (Routes & Nav)
              │           └── F-WF-07 (Real-Time Notifications)
              ├── F-WF-04 (Action Form Freeze)
              └── F-WF-05 (Action Detail Workflow UI)
```

---

## What Already Exists (Do NOT Recreate)

- `ActionItem` entity with statuses, dates, `CreatedByUserId`, `IsStandalone`, `IsEscalated`
- `ActionItemEscalation` entity for tracking escalations
- `ActionItemAssignee` junction table (multi-assignee)
- `KuEmployeeInfo` table (contains manager email/name — use for direct manager lookup)
- `INotificationService` + `AppNotification` entity (in-app notifications)
- `IEmailTemplateService` + `EmailTemplate` entity with `TemplateKey` lookup and `EmailLog`
- SignalR `NotificationHub` for real-time push
- `NotificationBellComponent` in header with unread count
- `NotificationsPageComponent` with grouping, filtering, mark-as-read
- `ActionStatus` enum: `ToDo, InProgress, OnHold, Overdue, Completed`
- Full permission system with `PermissionArea`, `PermissionAction`, `AreaPermissionMapping`
- Angular `NotificationService`, `ActionItemService`, `ToastService`
- SMTP email sending via MailKit

## What Is New

### Backend
- `ActionItemWorkflowRequest` entity (date change + status change requests)
- `WorkflowRequestType` enum (DateChangeRequest, StatusChangeRequest)
- `WorkflowRequestStatus` enum (Pending, Approved, Rejected)
- `ActionStatus` enum expanded: add `Deferred = 5`, `Cancelled = 6`
- `IActionItemWorkflowService` + implementation
- `IWorkflowNotificationHelper` + implementation (notification creation, SignalR push, email dispatch)
- `ActionItemWorkflowController` (10 endpoints)
- 6 new email templates seeded
- Hooks into existing `ActionItemService` (date freeze, status approval gate)
- Bypass methods for applying approved changes

### Frontend
- `WorkflowRequest`, `WorkflowRequestSummary`, etc. TypeScript models
- `WorkflowService` for API calls
- `WorkflowStateService` for shared pending count state
- `MyApprovalsComponent` (new page with two tabs)
- `WorkflowReviewDialogComponent` (shared, reusable)
- Updated `ActionFormComponent` (date lock, request buttons)
- Updated `ActionDetailComponent` (pending alerts, escalation direction, workflow history)
- Updated `HeaderComponent` (My Approvals nav link with badge)
- Updated SignalR handler (workflow-specific toast popups, clickable, badge refresh)
- New route `/approvals`
- Updated notification click routing for workflow action types

---

## Notification Flow Summary

### Who Gets Notified — And How

| Event                  | In-App Notification To               | Email To                                     | SignalR Push | Toast Style |
|------------------------|--------------------------------------|----------------------------------------------|-------------|-------------|
| Date change requested  | Creator + manager(s) of assignee(s)  | Creator + manager(s) — incl. non-registered  | ✅          | Info (blue)  |
| Date change approved   | Requester                            | Requester                                    | ✅          | Success (green) |
| Date change rejected   | Requester                            | Requester                                    | ✅          | Warning (amber) |
| Status change requested| Creator + manager(s) of assignee(s)  | Creator + manager(s) — incl. non-registered  | ✅          | Info (blue)  |
| Status change approved | Requester                            | Requester                                    | ✅          | Success (green) |
| Status change rejected | Requester                            | Requester                                    | ✅          | Warning (amber) |
| Escalation             | Creator + manager(s) of assignee(s)  | Creator + manager(s) — incl. non-registered  | ✅          | Error (red, 12s) |
| Direction given        | All assignees                        | All assignees                                | ✅          | Info (blue)  |

### Manager Resolution Path
```
Assignee (ApplicationUser)
  → email
    → KuEmployeeInfo (match by email)
      → manager email / manager name
        → ApplicationUser (match by manager email)
          → In-app notification (if account exists)
        → Email (always, even without account)
```

### Deduplication Rule
If a user appears in multiple recipient roles (e.g., creator who is also an assignee's manager), they receive exactly ONE notification per event, not multiple.
