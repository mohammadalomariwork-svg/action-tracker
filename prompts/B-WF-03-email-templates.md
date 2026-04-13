# B-WF-03 — Seed Email Templates for Workflow Events

## Context

The system already has `EmailTemplate` entity with `TemplateKey` lookup, `IEmailTemplateService`, and an `EmailLog` for delivery tracking. Email templates are managed in the admin UI. We need to seed 6 new templates for workflow events.

The existing email template seeder is in `ActionTracker.Infrastructure/Data/Seeders/` (check for the exact file name — it may be `EmailTemplateSeeder.cs` or part of `DatabaseSeeder.cs`). Find the existing seeder and add the new templates using the same pattern.

## Pre-requisite

- B-WF-02 completed (database is up to date)

## Instructions

### 1. Add 6 new email templates to the existing seeder

Locate the existing email template seeding logic and add 6 new templates with stable GUIDs. Use `NEWSEQUENTIALID()`-style ordered GUIDs or hardcoded GUIDs as the existing seeder does. Seed only if `TemplateKey` does not already exist (idempotent).

**Template placeholders convention (match existing templates):**
- `{{ActionItemCode}}` — the action item's ACT-NNN code
- `{{ActionItemTitle}}` — the action item title
- `{{RequesterName}}` — person who made the request
- `{{RecipientName}}` — person receiving the email
- `{{Reason}}` — justification text
- `{{CurrentStartDate}}`, `{{CurrentDueDate}}` — current dates before change
- `{{NewStartDate}}`, `{{NewDueDate}}` — proposed new dates
- `{{CurrentStatus}}`, `{{NewStatus}}` — current and proposed status
- `{{ReviewerName}}` — person who approved/rejected
- `{{ReviewComment}}` — reviewer's note
- `{{ActionUrl}}` — deep link to the action item in the frontend
- `{{ApprovalsUrl}}` — deep link to the My Approvals page

#### Template 1: `ActionItem.DateChangeRequested`
- **Name:** Action Item Date Change Requested
- **Subject:** Date Change Requested — {{ActionItemCode}}: {{ActionItemTitle}}
- **Body:** Inform the recipient that a date change has been requested on the action item. Show current dates, proposed dates, requester name, and reason. Include a link to the approvals page.

#### Template 2: `ActionItem.DateChangeReviewed`
- **Name:** Action Item Date Change Reviewed
- **Subject:** Date Change {{Outcome}} — {{ActionItemCode}}: {{ActionItemTitle}}
- **Body:** Inform the requester that their date change request was approved or rejected. Show reviewer name, review comment if any, and the outcome. Include a link to the action item.

#### Template 3: `ActionItem.StatusChangeRequested`
- **Name:** Action Item Status Change Requested
- **Subject:** Status Change Requested — {{ActionItemCode}}: {{ActionItemTitle}}
- **Body:** Inform the recipient that a status change has been requested. Show current status, proposed status, requester name, and reason. Include a link to the approvals page.

#### Template 4: `ActionItem.StatusChangeReviewed`
- **Name:** Action Item Status Change Reviewed
- **Subject:** Status Change {{Outcome}} — {{ActionItemCode}}: {{ActionItemTitle}}
- **Body:** Inform the requester that their status change was approved or rejected. Show reviewer name, review comment, outcome. Include a link to the action item.

#### Template 5: `ActionItem.Escalated`
- **Name:** Action Item Escalated
- **Subject:** Action Item Escalated — {{ActionItemCode}}: {{ActionItemTitle}}
- **Body:** Notify the creator and the direct manager that the action item has been escalated. Show the action item details, assignee name, escalation reason. Include a link to the action item. Mention they can add directions or comments.

#### Template 6: `ActionItem.DirectionGiven`
- **Name:** Action Item Direction Given
- **Subject:** Direction Given — {{ActionItemCode}}: {{ActionItemTitle}}
- **Body:** Notify the assignee that their manager or the creator has given direction on the escalated action item. Show who gave the direction and include a link to the action item to view the comment.

### 2. HTML body styling

Use the same HTML styling pattern as existing templates. If existing templates use a simple HTML structure with inline styles, follow the same pattern. The body should be clean, professional, and include:
- KU Action Tracker header
- A structured body with clear sections
- A prominent CTA button linking to `{{ApprovalsUrl}}` or `{{ActionUrl}}`
- A footer with "This is an automated message from KU Action Tracker"

### 3. Run the application to seed

Start the API (`dotnet run --project ActionTracker.API`) so the seeder executes on startup. Verify the 6 new rows exist in the `EmailTemplates` table.

## Validation

- 6 new rows in `EmailTemplates` table with the template keys listed above
- Existing email templates are untouched
- Templates are `IsActive = true` and `IsDeleted = false`
- Application starts without errors
