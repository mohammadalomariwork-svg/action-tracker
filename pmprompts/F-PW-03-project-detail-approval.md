# F-PW-03 — Project Detail: Submit for Approval + Approval History

## Context

The `ProjectDetailComponent` (`/projects/:id`) already shows project details, milestones, Gantt chart, and action items. Now we need to add:
1. A "Submit for Approval" button for the project manager when the project is in `Draft` status
2. An approval request history section showing all past and current approval requests
3. A review panel for sponsors/managers to approve or reject directly from the project detail page

## What to do

### 1. "Submit for Approval" button

Add a button in the project detail header/action area:
- **Label:** "Submit for Approval" with a `bi-send` icon
- **Visibility:** Only shown when ALL of these are true:
  - Project status is `Draft`
  - Current user is the project manager (`projectManagerUserId` matches current user ID)
  - User has `Projects.Edit` permission
- **On click:** Open a modal dialog asking for a justification reason (textarea, required, max 2000 chars). On submit:
  - Call `ProjectWorkflowService.submitForApproval({ projectId, reason })`
  - On success: show a success toast "Project submitted for approval", refresh the project detail to reflect the `PendingApproval` status
  - On error: show an error toast with the error message

### 2. "Pending Approval" status banner

When the project status is `PendingApproval`, display a prominent info banner below the project header:
- **Style:** Bootstrap `alert alert-warning` or similar attention-grabbing style
- **Text:** "This project is pending approval. Awaiting review from sponsors or management."
- **For reviewers:** If the current user can review (call `canReviewProject` on load), show "Approve" and "Reject" buttons within the banner

### 3. Review actions (inline on project detail)

If the current user can review the project (determined by `ProjectWorkflowService.canReviewProject(projectId)`):
- Show "Approve" and "Reject" buttons in the pending approval banner
- **Approve:** Opens the reusable `WorkflowReviewDialogComponent` (or a similar modal) pre-configured for approval. Comment is optional.
- **Reject:** Opens the same dialog pre-configured for rejection. Comment is required.
- On submit: call `ProjectWorkflowService.reviewApprovalRequest(requestId, dto)`
- On success: show toast, refresh project detail
- Get the pending request ID by loading approval requests for the project and finding the one with `Pending` status

### 4. Approval History section

Add a new collapsible section below the existing project sections (after Milestones, before or after Risk Register):
- **Section title:** "Approval History" with a `bi-clock-history` icon
- **Content:** A table or card list showing all `ProjectApprovalRequest` records for this project
- **Columns/fields:**
  - Status badge (Pending = warning, Approved = success, Rejected = danger)
  - Requested by (display name)
  - Reason
  - Reviewed by (display name, or "—" if pending)
  - Review comment (or "—" if pending)
  - Date submitted
  - Date reviewed (or "—" if pending)
- **Data source:** Call `ProjectWorkflowService.getApprovalRequestsForProject(projectId)` on component init
- Show "No approval requests yet" if the list is empty

### 5. Disable editing controls when not in Draft

When the project is in `PendingApproval`, `Active`, `Suspended`, or `Closed`:
- Hide or disable the "Edit Project" button (existing button)
- Keep the project detail view read-only for date fields
- The milestone "Add Milestone" button should be hidden
- The "Add Action Item" button within milestones should be hidden

This is a UI-only enforcement (the backend already blocks these operations from B-PW-06).

## Files to modify
- `ProjectDetailComponent` template and TypeScript — add submit button, approval banner, review actions, approval history section, and conditional controls
- Import `ProjectWorkflowService` and related models

## Files to create
- None (reuse existing `WorkflowReviewDialogComponent` pattern)

## Do NOT
- Do not create a new page — this is all within the existing project detail page
- Do not modify backend endpoints
- Do not change the Gantt chart or milestone section logic
