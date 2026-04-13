# F-PW-04 — Extend My Approvals Page with Project Approvals

## Context

The My Approvals page (`/approvals`, component `MyApprovalsComponent`) currently has two tabs: "Pending Reviews" and "My Requests" — both for action item workflow requests. We need to add project approval requests to this page.

## What to do

### 1. Add a third tab: "Project Approvals"

Extend the existing tab navigation in `MyApprovalsComponent` to include a third tab:
- **Tab label:** "Project Approvals"
- **Tab icon:** `bi-kanban` (or `bi-clipboard-check`)
- **Position:** After the existing "My Requests" tab

### 2. Project Approvals tab content

#### Stat cards
At the top of the tab, show stat cards (matching the style of the existing stat cards on the other tabs):
- **Pending Reviews** (count of pending project approval requests where current user is a reviewer) — warning color
- **My Submitted** (count of requests submitted by current user) — info color

#### Two sub-sections within the tab

**Sub-section A: "Pending Project Reviews"**
- Fetch data from `ProjectWorkflowService.getPendingReviews()`
- Display as a card list or table with:
  - Project code and name (clickable, navigates to `/projects/{projectId}`)
  - Requested by (display name)
  - Reason (truncated with tooltip for long text)
  - Date submitted (formatted with `date-fns`)
  - "Approve" button (green) and "Reject" button (red)
- Approve/Reject opens the `WorkflowReviewDialogComponent` configured for project approval
- On review complete: refresh the list, show success toast, decrement the header badge count

**Sub-section B: "My Project Requests"**
- Fetch data from `ProjectWorkflowService.getMyRequests()`
- Display as a card list or table with:
  - Project code and name (clickable)
  - Status badge (Pending/Approved/Rejected)
  - Reason submitted
  - Reviewer name (if reviewed)
  - Review comment (if reviewed)
  - Date submitted / Date reviewed
- This section is read-only (no action buttons)

### 3. Update the existing stat cards row

The existing stat cards at the top of the My Approvals page (outside the tabs) show combined pending counts. Add the pending project approvals count to the total:
- If there's a "Total Pending" card, include project approvals in the count
- If stat cards are tab-specific, add them within the new tab only
- Match the visual style of existing cards exactly

### 4. Update the header badge

The header navigation already shows a pending approval count badge on the "My Approvals" link. This count is managed by `WorkflowStateService`. After F-PW-01 updated the service to include project approvals, the badge should already reflect the combined total. Verify this works end-to-end.

### 5. SignalR integration

Listen for project workflow SignalR events to auto-refresh the project approvals tab:
- When a `ProjectApprovalRequested` notification arrives via SignalR and the current user is on the My Approvals page, refresh the pending reviews list
- When a `ProjectApprovalApproved` or `ProjectApprovalRejected` notification arrives, refresh the my requests list
- Follow the same SignalR subscription pattern used for action item workflow notifications

## Files to modify
- `MyApprovalsComponent` template and TypeScript — add the third tab, stat cards, review/request lists, and SignalR subscriptions
- Import `ProjectWorkflowService` and the project approval models

## Files to create
- None

## Do NOT
- Do not create a separate page — extend the existing My Approvals page
- Do not modify the existing action item workflow tabs
- Do not change backend endpoints
- Do not modify `WorkflowReviewDialogComponent` — reuse it as-is (it should already support a generic approve/reject flow)
