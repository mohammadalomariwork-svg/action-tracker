# B-IN-01 — AppNotification Entity, EF Configuration, Migration

## Context
We are adding an in-app notification system. Notifications are stored in the database and pushed to connected clients in real-time via SignalR. This prompt creates the entity and migration only.

## Requirements

### 1. Create entity `AppNotification` in `ActionTracker.Domain/Entities/`

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK, default `Guid.NewGuid()` |
| UserId | string (450) | Recipient user ID (no FK to AspNetUsers) |
| Title | string (300) | Notification title |
| Message | string (1000) | Notification body text |
| Type | string (50) | Category: `ActionItem`, `Project`, `Milestone`, `Workspace`, `Risk`, `Kpi`, `StrategicObjective`, `System` |
| ActionType | string (50) | What happened: `Created`, `Updated`, `StatusChanged`, `Completed`, `Assigned`, `Escalated`, `Overdue`, `Deleted` |
| RelatedEntityType | string? (100) | Entity type for navigation (e.g., "ActionItem", "Project") |
| RelatedEntityId | Guid? | Entity ID for navigation |
| RelatedEntityCode | string? (50) | Human-readable code (e.g., "ACT-001", "PRJ-2025-003") for display |
| Url | string? (500) | Frontend URL to navigate to when clicked |
| IsRead | bool | Default false |
| ReadAt | DateTime? | When the user marked it as read |
| CreatedAt | DateTime | UTC |
| CreatedByUserId | string? | Who triggered the notification (no FK) |
| CreatedByDisplayName | string? (200) | Denormalized display name of the actor |

### 2. Create EF configuration `AppNotificationConfiguration` in `ActionTracker.Infrastructure/Data/Configurations/`

- Table: `AppNotifications`
- No FK constraints — `UserId` and `CreatedByUserId` are plain strings
- Index on `UserId` + `IsRead` + `CreatedAt` descending (main query pattern)
- Index on `UserId` + `CreatedAt` descending
- Index on `RelatedEntityType` + `RelatedEntityId`

### 3. Register `DbSet<AppNotification>` in `ApplicationDbContext`

### 4. Create migration
Run: `dotnet ef migrations add AddAppNotifications --project ActionTracker.Infrastructure --startup-project ActionTracker.Api`

## Rules
- GUID primary key
- No FK to AspNetUsers — UserId is a plain string column
- No soft delete — notifications can be hard-deleted for cleanup (or retained indefinitely)
- UTC for all DateTime fields
- Do NOT create services, DTOs, controllers, or SignalR hubs in this prompt
