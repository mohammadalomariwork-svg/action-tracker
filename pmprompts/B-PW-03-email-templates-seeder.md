# B-PW-03 — Email Templates Seeder for Project Workflow

## Context

The system already has email templates seeded on startup for action item workflow notifications (e.g., `DateChangeRequested`, `StatusChangeReviewed`). The seeder is in `ActionTracker.Infrastructure`. We need to add new templates for project workflow notifications.

## What to do

### 1. Seed the following email templates

Add these templates to the existing email template seeding logic. Use the same pattern already established — check if the template key exists before inserting to avoid duplicates on re-run.

#### Template 1: `ProjectCreated`
- **Subject:** `New Project Created: {{ProjectCode}} — {{ProjectName}}`
- **HTML Body:** Inform the recipient that a new project has been created. Include placeholders:
  - `{{ProjectCode}}` — project code
  - `{{ProjectName}}` — project name
  - `{{ProjectType}}` — Operational or Strategic
  - `{{CreatedByName}}` — display name of the project manager
  - `{{WorkspaceName}}` — workspace name
  - `{{PlannedStartDate}}` — planned start date
  - `{{PlannedEndDate}}` — planned end date
  - `{{ProjectUrl}}` — deep link to the project detail page
- **Purpose:** Sent to direct line manager and all project sponsors when a project is first created.

#### Template 2: `ProjectApprovalRequested`
- **Subject:** `Project Approval Requested: {{ProjectCode}} — {{ProjectName}}`
- **HTML Body:** Inform the recipient that a project is awaiting their approval to start. Include placeholders:
  - `{{ProjectCode}}`
  - `{{ProjectName}}`
  - `{{RequestedByName}}` — display name of requester
  - `{{Reason}}` — justification for starting the project
  - `{{ProjectUrl}}` — deep link to the project detail page
  - `{{ApprovalUrl}}` — deep link to the approvals page
- **Purpose:** Sent to all sponsors and the direct line manager when the project manager submits a start-approval request.

#### Template 3: `ProjectApprovalReviewed`
- **Subject:** `Project {{Decision}}: {{ProjectCode}} — {{ProjectName}}`
- **HTML Body:** Inform the project manager of the approval decision. Include placeholders:
  - `{{ProjectCode}}`
  - `{{ProjectName}}`
  - `{{Decision}}` — "Approved" or "Rejected"
  - `{{ReviewedByName}}` — display name of the reviewer
  - `{{ReviewComment}}` — reviewer's comment (may be empty for approvals)
  - `{{ProjectUrl}}` — deep link to the project detail page
- **Purpose:** Sent to the project manager after a sponsor or manager approves or rejects the request.

### 2. Template IDs

Use hardcoded `Guid` values for idempotent seeding (matching the existing pattern). Generate three new sequential GUIDs and use them consistently.

## Files to modify
- The existing email template seeder file in `ActionTracker.Infrastructure/` (find the file that seeds `EmailTemplate` records — it may be in a `Seeders/` or `Data/Seeders/` folder)

## Files to create
- None (modify existing seeder only)

## Do NOT
- Do not modify the `EmailTemplate` entity
- Do not create new seeder classes — add to the existing one
- Do not delete or modify existing template seeds
