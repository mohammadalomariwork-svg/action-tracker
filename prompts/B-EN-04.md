# B-EN-04 — Wire Email Sending Into Existing Services

## Context
B-EN-01 through B-EN-03 created the email infrastructure: entities, MailKit sender, template service, and seeder. This prompt wires the `IEmailSender` into the existing business services to trigger emails on key events.

## Requirements

### Inject `IEmailSender` into the following existing services

This prompt MODIFIES existing service files. For each service, inject `IEmailSender` via constructor and add email-sending calls AFTER the successful database operation.

### 1. `ActionItemService`

**On Create** (after save):
- Send `ActionItem.Created` to the creator's email
- Send `ActionItem.Assigned` to each assignee's email (if assignees exist)
- Placeholders: ActionId, Title, Description, Status, Priority, DueDate, Progress, AssignedTo (comma-separated names), CreatedBy, WorkspaceName, ProjectName, ItemUrl

**On Status Change** (after save):
- Send `ActionItem.StatusChanged` to creator + all assignees
- If new status is "Done": also send `ActionItem.Completed` to creator + all assignees + project manager (if linked to project)
- If new status is "Overdue": send `ActionItem.Overdue` to creator + all assignees

**On Escalation** (when IsEscalated set to true):
- Send `ActionItem.Escalated` to creator + all assignees

**Recipient resolution:** Look up `ApplicationUser.Email` for each user ID from the database. Skip users with no email or inactive users.

### 2. `ProjectService`

**On Create** (after save):
- Send `Project.Created` to the project manager's email
- Placeholders: ProjectCode, ProjectName, Description, Status, Priority, ProjectManager, WorkspaceName, PlannedStartDate, PlannedEndDate, Budget, ItemUrl

**On Status Change** (after save):
- Send `Project.StatusChanged` to the project manager + all sponsors
- If new status is "Completed": also send `Project.Completed` to the project manager + all sponsors

### 3. `MilestoneService`

**On Create** (after save):
- Send `Milestone.Created` to the project manager of the parent project
- Placeholders: MilestoneCode, MilestoneName, ProjectName, ProjectCode, Status, PlannedDueDate, CompletionPercentage, ItemUrl

**On Status Change to Completed** (after save):
- Send `Milestone.Completed` to the project manager + milestone approver (if set)

### 4. `WorkspaceService`

**On Create** (after save):
- Send `Workspace.Created` to all workspace admins
- Placeholders: WorkspaceName, OrgUnit, CreatedBy, ItemUrl

### 5. `StrategicObjectiveService`

**On Create** (after save):
- Send `StrategicObjective.Created` to the creator
- Placeholders: ObjectiveCode, Statement, OrgUnit, CreatedBy, ItemUrl

### 6. `KpiService`

**On Create** (after save):
- Send `Kpi.Created` to the creator
- Placeholders: KpiName, ObjectiveName, ObjectiveCode, CalculationMethod, Period, CreatedBy, ItemUrl

### 7. `ProjectRiskService` (created in B-RR-02)

**On Create** (after save):
- Send `Risk.Created` to the risk owner + project manager
- If RiskRating is "Critical": also send `Risk.Critical` to the project manager + all project sponsors
- Placeholders: RiskCode, Title, Description, Category, RiskScore, RiskRating, Status, ProjectName, ProjectCode, RiskOwner, DueDate, MitigationPlan, ItemUrl

**On Status Change** (after save):
- Send `Risk.StatusChanged` to the risk owner + project manager

### ItemUrl Construction

Build the frontend URL for the "View Details" button in emails. Use a configurable base URL from appsettings:

```json
"App": {
  "FrontendBaseUrl": "http://localhost:4200"
}
```

URL patterns:
- Action Item: `{FrontendBaseUrl}/actions/{id}/view`
- Project: `{FrontendBaseUrl}/projects/{id}`
- Milestone: `{FrontendBaseUrl}/projects/{projectId}/milestones/{milestoneId}`
- Workspace: `{FrontendBaseUrl}/workspaces/{id}`
- Risk: `{FrontendBaseUrl}/projects/{projectId}/risks/{riskId}`
- Strategic Objective: `{FrontendBaseUrl}/admin/objectives`
- KPI: `{FrontendBaseUrl}/admin/kpis`

### Critical Implementation Rules

1. **Fire-and-forget:** Email sending MUST NOT delay the API response. Use `_ = Task.Run(async () => { ... })` or similar pattern to send emails in the background after the DB transaction commits.
2. **Never throw on email failure:** The `IEmailSender` already swallows exceptions, but the calling code should also be wrapped in try-catch as defense.
3. **Recipient deduplication:** If the same user appears in multiple recipient lists (e.g., creator is also assignee), send only ONE email per unique email address.
4. **Skip inactive templates:** `IEmailSender` already handles this, but callers should not worry about template state.
5. **Do not send emails for soft-delete or restore operations.**

## Rules
- Modify existing service files (inject IEmailSender, add calls)
- All email sending is background/fire-and-forget
- Never break the main business operation due to email
- Deduplicate recipients
- All async
