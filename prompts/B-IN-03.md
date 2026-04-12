# B-IN-03 — Wire In-App Notification Creation Into Existing Services

## Context
B-IN-02 created the NotificationService and SignalR hub. This prompt wires `INotificationService` into existing business services to create in-app notifications on key events. These notifications mirror the email events but are stored as in-app records.

## Requirements

### Inject `INotificationService` into the following existing services

This prompt MODIFIES existing service files. For each service, inject `INotificationService` via constructor and add notification creation calls AFTER the successful database operation.

### Notification Patterns

For each event, create `CreateNotificationDto` entries for the relevant recipients. Use `CreateBulkAsync` when multiple users need to be notified.

### 1. `ActionItemService`

**On Create:**
- Notify each assignee: Title = "New Action Item Assigned", Message = "You've been assigned to {{Title}} ({{ActionId}})", Type = "ActionItem", ActionType = "Assigned", Url = `/actions/{id}/view`

**On Status Change:**
- Notify creator + all assignees: Title = "Status Updated", Message = "{{ActionId}} status changed to {{Status}}", Type = "ActionItem", ActionType = "StatusChanged"
- If Done: Title = "Action Item Completed", ActionType = "Completed"
- If Overdue: Title = "Action Item Overdue", ActionType = "Overdue"

**On Escalation:**
- Notify creator + all assignees: Title = "Action Item Escalated", ActionType = "Escalated"

### 2. `ProjectService`

**On Create:**
- Notify project manager: Title = "New Project", Message = "Project {{ProjectName}} ({{ProjectCode}}) has been created", Type = "Project", ActionType = "Created", Url = `/projects/{id}`

**On Status Change:**
- Notify project manager + all sponsors: Title = "Project Status Updated", ActionType = "StatusChanged"
- If Completed: Title = "Project Completed", ActionType = "Completed"

### 3. `MilestoneService`

**On Create:**
- Notify project manager: Title = "New Milestone", Message = "{{MilestoneName}} added to {{ProjectName}}", Type = "Milestone", ActionType = "Created", Url = `/projects/{projectId}/milestones/{milestoneId}`

**On Completed:**
- Notify project manager + approver: Title = "Milestone Completed", ActionType = "Completed"

### 4. `WorkspaceService`

**On Create:**
- Notify all workspace admins: Title = "New Workspace", Message = "Workspace {{WorkspaceName}} has been created", Type = "Workspace", ActionType = "Created", Url = `/workspaces/{id}`

### 5. `StrategicObjectiveService`

**On Create:**
- Notify creator (self-notification for confirmation): Title = "Strategic Objective Created", Type = "StrategicObjective", ActionType = "Created"

### 6. `KpiService`

**On Create:**
- Notify creator: Title = "KPI Created", Type = "Kpi", ActionType = "Created"

### 7. `ProjectRiskService`

**On Create:**
- Notify risk owner + project manager: Title = "New Risk Identified", Message = "{{RiskCode}}: {{Title}} — Rating: {{RiskRating}}", Type = "Risk", ActionType = "Created", Url = `/projects/{projectId}/risks/{riskId}`
- If Critical: additional notification with Title = "Critical Risk Alert", ActionType = "Escalated"

**On Status Change:**
- Notify risk owner + project manager: Title = "Risk Status Updated", ActionType = "StatusChanged"

### Critical Implementation Rules

1. **Do NOT notify the actor:** If user A creates an action item and assigns it to user B and user C, only user B and user C receive notifications — not user A (unless user A is also an assignee). Compare `CreatedByUserId` against each recipient and skip matches.
2. **Fire-and-forget:** Notification creation MUST NOT delay the API response. Use background execution: `_ = Task.Run(async () => { ... })` with try-catch.
3. **Never throw on notification failure:** Wrap all notification calls in try-catch. Log errors but never propagate.
4. **Recipient deduplication:** If the same user appears in multiple roles (e.g., project manager is also a sponsor), send only ONE notification.
5. **Populate all fields:** Always set `RelatedEntityCode` (e.g., "ACT-001"), `RelatedEntityType`, `RelatedEntityId`, `Url`, `CreatedByUserId`, `CreatedByDisplayName`.
6. **Do not send notifications for soft-delete or restore operations.**

## Rules
- Modify existing service files (inject INotificationService, add calls)
- Notification sending is background/fire-and-forget
- Never break the main operation due to notification failure
- Exclude the actor from their own notifications
- Deduplicate recipients
- All async
