# B-EN-03 — EmailTemplateSeeder — Seed Default Email Templates

## Context
B-EN-02 created the email template service and sender. This prompt seeds the database with all default email templates on startup.

## Requirements

### Create `EmailTemplateSeeder` in `ActionTracker.Infrastructure/Data/Seeders/`

Seed the following templates. Each template has a stable hardcoded GUID (generate unique GUIDs for each). Use upsert logic: insert if TemplateKey doesn't exist, skip if it already exists (do NOT overwrite admin edits).

| TemplateKey | Name | Subject | When Sent |
|------------|------|---------|-----------|
| `ActionItem.Created` | Action Item Created | `New Action Item: {{Title}} ({{ActionId}})` | When a new action item is created |
| `ActionItem.Assigned` | Action Item Assigned | `You've been assigned: {{Title}} ({{ActionId}})` | When a user is assigned to an action item |
| `ActionItem.StatusChanged` | Action Item Status Changed | `Action Item {{ActionId}} status changed to {{Status}}` | When status changes |
| `ActionItem.Completed` | Action Item Completed | `Action Item Completed: {{Title}} ({{ActionId}})` | When status changes to Done |
| `ActionItem.Overdue` | Action Item Overdue | `Action Item Overdue: {{Title}} ({{ActionId}})` | When item is marked overdue |
| `ActionItem.Escalated` | Action Item Escalated | `Action Item Escalated: {{Title}} ({{ActionId}})` | When item is escalated |
| `Project.Created` | Project Created | `New Project: {{ProjectName}} ({{ProjectCode}})` | When a project is created |
| `Project.StatusChanged` | Project Status Changed | `Project {{ProjectCode}} status changed to {{Status}}` | When project status changes |
| `Project.Completed` | Project Completed | `Project Completed: {{ProjectName}} ({{ProjectCode}})` | When project status changes to Completed |
| `Milestone.Created` | Milestone Created | `New Milestone: {{MilestoneName}} ({{MilestoneCode}})` | When a milestone is created |
| `Milestone.Completed` | Milestone Completed | `Milestone Completed: {{MilestoneName}} ({{MilestoneCode}})` | When milestone is completed |
| `Workspace.Created` | Workspace Created | `New Workspace: {{WorkspaceName}}` | When a workspace is created |
| `StrategicObjective.Created` | Strategic Objective Created | `New Strategic Objective: {{ObjectiveCode}} — {{Statement}}` | When an objective is created |
| `Kpi.Created` | KPI Created | `New KPI: {{KpiName}} for {{ObjectiveName}}` | When a KPI is created |
| `Risk.Created` | Risk Created | `New Risk: {{RiskCode}} — {{Title}} ({{ProjectName}})` | When a risk is identified |
| `Risk.StatusChanged` | Risk Status Changed | `Risk {{RiskCode}} status changed to {{Status}}` | When risk status changes |
| `Risk.Critical` | Critical Risk Alert | `Critical Risk Alert: {{RiskCode}} — {{Title}}` | When a risk is rated Critical |

### HTML Body Template Pattern

Each template body should follow this pattern (use appropriate fields per template):

```html
<div style="font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto;">
  <h2 style="color: #0F52BA;">{{Title}}</h2>
  <p>{{Description}}</p>
  <table style="width: 100%; border-collapse: collapse; margin: 16px 0;">
    <tr><td style="padding: 8px; font-weight: 600; width: 140px;">Code:</td><td style="padding: 8px;">{{Code}}</td></tr>
    <tr><td style="padding: 8px; font-weight: 600;">Status:</td><td style="padding: 8px;">{{Status}}</td></tr>
    <tr><td style="padding: 8px; font-weight: 600;">Priority:</td><td style="padding: 8px;">{{Priority}}</td></tr>
    <tr><td style="padding: 8px; font-weight: 600;">Due Date:</td><td style="padding: 8px;">{{DueDate}}</td></tr>
    <tr><td style="padding: 8px; font-weight: 600;">Assigned To:</td><td style="padding: 8px;">{{AssignedTo}}</td></tr>
  </table>
  <a href="{{ItemUrl}}" style="display: inline-block; padding: 10px 24px; background-color: #0F52BA; color: #fff; text-decoration: none; border-radius: 4px;">View Details</a>
</div>
```

Customize the table rows per template type (projects show ProjectManager, workspace shows OrgUnit, risks show RiskScore and RiskRating, etc.).

### Register seeder in startup
Call `EmailTemplateSeeder` in the same startup seeding pipeline, AFTER `DefaultRolePermissionsSeeder`. This modifies the existing startup/seeding code.

## Available Placeholders Per Template

**ActionItem:** `{{ActionId}}`, `{{Title}}`, `{{Description}}`, `{{Status}}`, `{{Priority}}`, `{{DueDate}}`, `{{Progress}}`, `{{AssignedTo}}`, `{{CreatedBy}}`, `{{WorkspaceName}}`, `{{ProjectName}}`, `{{ItemUrl}}`

**Project:** `{{ProjectCode}}`, `{{ProjectName}}`, `{{Description}}`, `{{Status}}`, `{{Priority}}`, `{{ProjectManager}}`, `{{WorkspaceName}}`, `{{PlannedStartDate}}`, `{{PlannedEndDate}}`, `{{Budget}}`, `{{ItemUrl}}`

**Milestone:** `{{MilestoneCode}}`, `{{MilestoneName}}`, `{{ProjectName}}`, `{{ProjectCode}}`, `{{Status}}`, `{{PlannedDueDate}}`, `{{CompletionPercentage}}`, `{{ItemUrl}}`

**Workspace:** `{{WorkspaceName}}`, `{{OrgUnit}}`, `{{CreatedBy}}`, `{{ItemUrl}}`

**StrategicObjective:** `{{ObjectiveCode}}`, `{{Statement}}`, `{{OrgUnit}}`, `{{CreatedBy}}`, `{{ItemUrl}}`

**KPI:** `{{KpiName}}`, `{{ObjectiveName}}`, `{{ObjectiveCode}}`, `{{CalculationMethod}}`, `{{Period}}`, `{{CreatedBy}}`, `{{ItemUrl}}`

**Risk:** `{{RiskCode}}`, `{{Title}}`, `{{Description}}`, `{{Category}}`, `{{RiskScore}}`, `{{RiskRating}}`, `{{Status}}`, `{{ProjectName}}`, `{{ProjectCode}}`, `{{RiskOwner}}`, `{{DueDate}}`, `{{MitigationPlan}}`, `{{ItemUrl}}`

## Rules
- Use hardcoded stable GUIDs for each template
- Upsert logic — do not overwrite templates that already exist
- HTML bodies must be inline-styled (no CSS classes) for email client compatibility
- Use `#0F52BA` (sapphire blue) as the brand color in templates
- All seeding async
