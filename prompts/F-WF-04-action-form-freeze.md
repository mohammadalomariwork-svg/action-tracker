# F-WF-04 — Update Action Form for Date Freeze & Status Change

## Context

The existing `ActionFormComponent` (in `src/app/features/actions/` or similar) is an offcanvas/drawer form for creating and editing action items. When editing a standalone action item, the date fields should be read-only (frozen) and the status dropdown should not allow direct transition to terminal statuses. Instead, the form should show buttons to initiate workflow requests.

## Pre-requisite

- F-WF-02 completed (WorkflowService exists)
- F-WF-01 completed (models exist)

## Instructions

### 1. Modify `ActionFormComponent`

**File:** Find the existing action form component (likely `src/app/features/actions/action-form/action-form.component.ts`)

#### Add new dependencies:
- Inject `WorkflowService`
- Add new state variables:
  - `isEditMode: boolean` — true when editing an existing item
  - `isStandalone: boolean` — true when the action item is standalone
  - `areDatesLocked: boolean` — `isEditMode && isStandalone`
  - `showDateChangeDialog: boolean`
  - `showStatusChangeDialog: boolean`
  - `pendingRequests: WorkflowRequest[]` — load existing pending requests for this item
  - `canReview: boolean` — whether the current user can review

#### On edit load:
1. After loading the action item data, check if `isStandalone` is true
2. If standalone and editing: set `areDatesLocked = true`
3. Load pending workflow requests: `workflowService.getRequestsForActionItem(actionItemId)`
4. Check review capability: `workflowService.canReview(actionItemId)`

### 2. Template Changes

#### Date Fields (Start Date and Due Date)

When `areDatesLocked` is true:
- Make the date input fields **read-only** (add `[readonly]="true"` or disable them)
- Show a lock icon (🔒) next to each date field using Bootstrap Icons `bi-lock-fill`
- Below the date fields, show a link/button: **"Request Date Change"**
- When clicked, open a small inline form or modal with:
  - New Start Date (optional)
  - New Due Date (optional)  
  - Reason (required, textarea)
  - Submit button
- On submit: call `workflowService.createDateChangeRequest(dto)`, show toast, close the dialog

When creating a new action item (`!isEditMode`): date fields behave normally (no lock).

When editing a non-standalone item: date fields behave normally (no lock).

#### Status Field

When `isStandalone && isEditMode`:
- Show the current status as a badge (read-only display)
- Below, show status transition buttons based on current status:
  - If current status is `ToDo`: allow direct change to `InProgress` (no workflow needed)
  - If current status is `InProgress` or `ToDo` or `OnHold` or `Overdue`: show a **"Request Status Change"** button that opens a dialog for Completed/OnHold/Deferred/Cancelled
  - Do NOT show Completed, OnHold, Deferred, Cancelled as direct options in the dropdown

**Status change request dialog:**
- Target status selector (dropdown with: Completed, OnHold, Deferred, Cancelled)
- Reason (required, textarea)
- Submit button
- On submit: call `workflowService.createStatusChangeRequest(dto)`, show toast

**Direct transitions that are allowed without workflow (standalone):**
- `ToDo` → `InProgress` (just starting work)

**All other transitions require a request.**

For non-standalone items: status field works as before (no restrictions).

### 3. Pending Requests Banner

If there are pending workflow requests for this action item, show an info banner at the top of the form:

```html
<div class="alert alert-info">
  <i class="bi bi-hourglass-split"></i>
  There are {{pendingRequests.length}} pending workflow request(s) for this action item.
  <a routerLink="/approvals">View in My Approvals</a>
</div>
```

If the current user can review the pending requests, show inline approve/reject buttons right in the banner.

### 4. Escalation Enhancement

If there's an existing escalation toggle/button in the form:
- When escalation is triggered, instead of just setting a flag, show a dialog asking for the escalation reason
- Call `workflowService.escalate(actionItemId, reason)` to trigger the full workflow with notifications
- Show a toast confirming that the creator and direct manager have been notified

### 5. Error Handling

When the backend returns a 422 error (trying to directly change dates or status), the `errorInterceptor` will show a toast. But also handle it gracefully in the component:
- Catch 422 errors from the update call
- If the error message contains "dates are locked", show the date change request dialog automatically
- If the error message contains "requires approval", show the status change request dialog automatically

### 6. Workflow Request History

Below the form (or in a collapsible section), show the workflow request history for this action item:
- List of past requests with type, status, requester, reviewer, dates
- Use a compact card or table layout
- This helps the user see what changes have been requested and their outcomes

## Validation

- Creating a new standalone action item: dates are editable, status is editable
- Editing a standalone action item: dates are locked with 🔒 icon, "Request Date Change" button visible
- Editing a standalone action item: status shows request button for terminal transitions
- Editing a non-standalone action item: everything works as before
- Date change request submits successfully and shows toast
- Status change request submits successfully and shows toast
- Pending requests banner shows when there are pending requests
- `ng build` passes
